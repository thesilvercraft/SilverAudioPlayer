using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using Avalonia.Collections;
using FuzzySharp;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using Swordfish.NET.Collections;
using Swordfish.NET.Collections.Auxiliary;
using Logger = Serilog.Core.Logger;

namespace SilverAudioPlayer.Core;

public class Logic<T> where T : PlayerContext
{
    public readonly List<IMusicStatusInterface> musicStatusInterfaces = new();
    private bool ChangeAllowed = true;
    private bool IsSortRequested;
    public Song? NextSong;
    public Stack<Song> SongHistory = new(26);
    public bool StopAutoLoading;
    private readonly CancellationTokenSource? token = new();

    public Logic(T playercontext)
    {
        playerContext = playercontext;
        playerContext.VolumeChanged ??= vol =>
        {
            Player?.SetVolume(vol);
            playerContext.RaiseAndSetIfChanged(ref playerContext._Volume, vol);
        };
        playerContext.GetVolume ??= () => playerContext._Volume;
        playerContext.GetLoopType ??= () => playerContext._LoopType;
        playerContext.SetLoopType ??= lt => playerContext.RaiseAndSetIfChanged(ref playerContext._LoopType, lt);
        ConcurrentObservableCollection<Song> _queue = new();
        playercontext.GetQueue ??= () => _queue;

        ChoosePlayProvider ??= (x, _) => Task.FromResult(x.FirstOrDefault());
    }

    public Action<Action> DoQueueManipulationAction = (a) => { Task.Run(a); };

    [ImportMany] public IEnumerable<IPlayProvider> PlayProviders { get; set; }

    [ImportMany] public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }

    [ImportMany] public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }

    [ImportMany] public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
    [ImportMany] public IEnumerable<IPlayStreamProvider> PlayStreamProviders { get; set; }
    [ImportMany] public IEnumerable<ISyncPlugin> SyncPlugins { get; set; }

    [Import] public IWillProvideMemory MemoryProvider { get; set; }


    public Func<IEnumerable<IPlayProvider>, WrappedStream, Task<IPlayProvider?>> ChoosePlayProvider { get; set; }

    public Logger log { get; set; }
    public T playerContext { get; set; }
    public List<MimeType> PlayableMimes { get; set; }

    private byte Volume
    {
        get => playerContext.Volume;
        set => playerContext.Volume = value;
    }

    public IPlay? Player { get; set; }


    IMusicStatusInterfaceListenerAdmin MusicStatusInterface;

    public void AddMSI(IMusicStatusInterface e, IMusicStatusInterfaceListenerAdmin musicStatusInterface)
    {
        musicStatusInterfaces.Add(e);
        MusicStatusInterface = musicStatusInterface;
        e.StartIPC(musicStatusInterface);
    }


    private void SendIfStateIsNotNull()
    {
        var state = Player?.GetPlaybackState();
        PlaybackStateChangedNotification(state != null ? state.Value : PlaybackState.Stopped);
    }

    /// <summary>
    ///     Lets music status interfaces know about a track change
    /// </summary>
    /// <param name="playerContext.CurrentSong">The new track</param>
    public void TrackChangedNotification(Song? currentSong)
    {
        MusicStatusInterface?.FireTrackChangedNotification(currentSong!);
    }

    /// <summary>
    ///     Lets music status interfaces know about a playstate change
    /// </summary>
    /// <param name="s">The new Playstate</param>
    public void PlaybackStateChangedNotification(PlaybackState s)
    {
        MusicStatusInterface?.FirePlayerStateChanged(s);
        switch (s)
        {
            case PlaybackState.Stopped:
                Parallel.ForEach(WakeLockInterfaces, msI => msI.UnWakeLock());
                break;
            case PlaybackState.Playing:
                Parallel.ForEach(WakeLockInterfaces, msI => msI.WakeLock());
                break;
        }
    }


    public void RemoveMsi(IMusicStatusInterface e, IMusicStatusInterfaceListener musicStatusInterface)
    {
        e.StopIPC(musicStatusInterface);
        e.Dispose();
        musicStatusInterfaces.Remove(e);
    }

    public void PlayPause(bool allowStart)
    {
        if (Player?.GetPlaybackState() == PlaybackState.Playing)
            Pause();
        else if (Player?.GetPlaybackState() == PlaybackState.Paused)
            Play();
        else if (allowStart) Play();
    }


    public void Next()
    {
        if (!ChangeAllowed || playerContext.CurrentSong == null) return;
        var a = playerContext.Queue.IndexOf(playerContext.CurrentSong);
        if (a == -1 || a + 1 >= playerContext.Queue.Count) return;
        ChangeAllowed = false;
        HandleSongChanging(playerContext.Queue[a + 1], true);
        ChangeAllowed = true;
    }

    public void Previous()
    {
        if (!ChangeAllowed || playerContext.CurrentSong == null) return;
        var a = playerContext.Queue.IndexOf(playerContext.CurrentSong);
        if (a == -1 || a - 1 < 0) return;
        ChangeAllowed = false;
        HandleSongChanging(playerContext.Queue[a - 1], true);
        ChangeAllowed = true;
    }

    public void MainWindow_Opened(IMusicStatusInterfaceListenerAdmin musicStatusInterface)
    {
        if (MusicStatusInterfaces?.Any() == true)
            Parallel.ForEach(MusicStatusInterfaces, dangthing =>
            {
                var a = dangthing;
                GC.KeepAlive(a);
                AddMSI(a, musicStatusInterface);
            });
    }


    public void Play()
    {
        if (Player != null)
        {
            Player?.Play();
            SendIfStateIsNotNull();
        }
        else
        {
            StartPlaying();
        }
    }

    public void Pause()
    {
        Player?.Pause();
        SendIfStateIsNotNull();
    }

    public async void StartPlaying(bool play = true, bool resetsal = false)
    {
        if (playerContext.CurrentSong == null)
        {
            if (playerContext.Queue.Count > 0)
            {
                playerContext.CurrentSong = playerContext.Queue[0];
            }
            else
            {
                Log.Information("Avoiding fatal crash, nothing to play");
                return;
            }
        }

        Player = await GetPlayerFromStream(playerContext.CurrentSong.Stream);

        if (Player == null)
        {
            playerContext?.ShowMessageBox?.Invoke("Error",
                "I do not know how to play " + playerContext.CurrentSong.URI);
            return;
        }

        Log.Information("Got player of type {PlayerType}", Player.GetType());

        var trackchangedtask = Task.Run(() => TrackChangedNotification(playerContext.CurrentSong));
        if (play)
        {
            Player.SetVolume(Volume);
            Player.Play();
            SendIfStateIsNotNull();
            Player.TrackEnd += OutputDevice_PlaybackStopped;
            playerContext?.ResetUIScrollBar?.Invoke();
            if (Player?.Length() is TimeSpan totalusable) playerContext?.SetScrollBarTextTo?.Invoke(totalusable);
            playerContext?.HandleLateStageMetadataAndScrollBar?.Invoke();
        }

        if (!resetsal) return;
        Log.Information("StopAutoLoading set to false in StartPlaying");
        StopAutoLoading = false;
    }

    public void RemoveTrack()
    {
        Player?.Stop();
        SendIfStateIsNotNull();
        Player = null;
        token?.Cancel();
        Thread.Sleep(30);
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        Log.Information(@"Output device playback stopped
StopAutoLoading: {StopAutoLoading}
Current song: {CurrentSong}
Loop mode: {LoopType}", StopAutoLoading, playerContext.CurrentSong, playerContext.LoopType);

        if (playerContext.LoopType == RepeatState.One && !StopAutoLoading)
        {
            //Loop
            StartPlaying();
        }
        else if (playerContext.CurrentSong != null && !StopAutoLoading)
        {
            var a = playerContext.Queue.IndexOf(playerContext.CurrentSong);
            if (a != -1)
            {
                if (a + 1 < playerContext.Queue.Count)
                {
                    HandleSongChanging(playerContext.Queue[a + 1], true);
                }
                else if (playerContext.LoopType == RepeatState.Queue)
                {
                    HandleSongChanging(playerContext.Queue[0], true);
                }
                else
                {
                    Log.Information("RemoveTrack in PlaybackStopped");
                    RemoveTrack();
                }
            }
            else if (NextSong != null)
            {
                HandleSongChanging(NextSong, true);
                NextSong = null;
            }
        }
        Log.Information("StopAutoLoading set to false in OutputDevice_PlaybackStopped");
        StopAutoLoading = false;
    }

    public void HandleSongChanging(Song nextSong, bool resetsal = false)
    {
        Log.Information("StopAutoLoading set to true in HandleSongChanging");
        StopAutoLoading = true;
        SongHistory.Push(playerContext.CurrentSong);
        playerContext.CurrentSong = nextSong;
        Debug.Assert(playerContext.CurrentSong == nextSong);
        RemoveTrack();
        StartPlaying(resetsal: resetsal);
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        var enumerable = files.ToList();
        if (enumerable?.Any() != true) return;
        var oneFile = enumerable.Count == 1;
        if (oneFile && Directory.Exists(enumerable.First()))
        {
            enumerable = FilterFiles(Directory.GetFiles(enumerable.First(), "*", SearchOption.AllDirectories)).ToList();
            oneFile = false;
        }

        enumerable = FilterFiles(enumerable).ToList();
        foreach (var path in enumerable)
        {
            if (Directory.Exists(path))
            {
                var files2 = FilterFiles(Directory.GetFiles(path, "*", SearchOption.AllDirectories));

                foreach (var file in files2)
                {
                    AddSong(new Song(file, file, Guid.NewGuid()), !oneFile);
                }
            }
            else
            {
                AddSong(new Song(path, path, Guid.NewGuid()), !oneFile);
            }
        }

        if (IsSortRequested) return;
        IsSortRequested = true;
        var sortTask = Task.Run(async () => await DoSort());
    }

    public void ProcessStreams(IEnumerable<WrappedStream> streams)
    {
        if (streams is null)
        {
            throw new ArgumentNullException(nameof(streams));
        }

        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Any() != true) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var file in wrappedStreams) AddSong(new Song(file, "unknown", Guid.NewGuid()), true);
        if (oneFile || IsSortRequested) return;
        IsSortRequested = true;
        var sortTask = Task.Run(async () => await DoSort());
    }

    public void ProcessStream(WrappedStream stream)
    {
        if (stream != null) AddSong(new Song(stream, "unknown", Guid.NewGuid()));
    }

    public void ClearAll()
    {
        playerContext.Queue.Clear();
    }

    private void AddSong(Song song, bool expectmore = false)
    {
        song.Metadata ??= Task.Run(async () => await GetMetadataFromStream(song.Stream)!).GetAwaiter().GetResult();
        playerContext.Queue.Add(song);
        if (expectmore || IsSortRequested) return;
        IsSortRequested = true;
        var sortTask = Task.Run(async () => await DoSort());
    }

    public async Task DoSort()
    {
        await Task.Delay(200);
        List<Song> sng = new();
        var albums = playerContext.Queue.ToList().GroupBy(a => a.Metadata?.Album);
        List<Tuple<string?, List<Song>>> fuzzedAlbums = new();
        foreach (var album in albums)
            if (fuzzedAlbums.Find(x => Fuzz.Ratio(x.Item1 ?? "", album.Key ?? "") > 80) is { } group)
                group.Item2.AddRange(album.ToList());
            else
                fuzzedAlbums.Add(new Tuple<string?, List<Song>>(album.Key, album.ToList()));
        foreach (var disc in fuzzedAlbums.Select(album => album.Item2.GroupBy(a => a?.Metadata?.DiscNumber ?? 0))
                     .SelectMany(discs => discs.OrderBy(a => a.Key)))
        {
            sng.AddRange(disc.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
        }

        Log.Information("Sorted through {Count} songs", sng.Count);
        DoQueueManipulationAction.Invoke(() =>
        {
            playerContext.Queue.Clear();
            foreach (var song in sng)
            {
                playerContext.Queue.Add(song);
            }
        });
        IsSortRequested = false;
    }

    public async Task<IPlay?> GetPlayerFromStream(WrappedStream stream)
    {
        return (await ChoosePlayProvider(PlayProviders?.Where(x => x.CanPlayFile(stream)), stream))?.GetPlayer(stream);
    }


    public async Task<IMetadata?>? GetMetadataFromStream(WrappedStream stream)
    {
        return new MetadataCombo(MetadataProviders?.Where(x => x.CanGetMetadata(stream))
            .Select(async x => (await x.GetMetadata(stream))).Select(t => t.Result).Where(i => i != null).ToList());
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return files.Where(x => !(
            x.EndsWith(".png")
            || x.EndsWith(".txt")
            || x.EndsWith(".docx")
            || x.EndsWith(".pdf")
            || x.EndsWith(".csv")
            || x.EndsWith(".jpg")
            || x.EndsWith(".lnk")
            || x.EndsWith(".md")
            || x.EndsWith(".zip")
            || x.EndsWith(".7z")
            || x.EndsWith(".rar")
            || x.EndsWith(".exe")
            || x.EndsWith(".dll")
            || x.EndsWith(".json")
            || x.EndsWith(".toml")
            || x.EndsWith(".yaml")
            || x.EndsWith(".xml")
            || x.EndsWith(".nfo")
            || x.EndsWith(".html")
            || x.EndsWith(".m3u")
            || x.EndsWith(".xmp")
            || x.EndsWith(".log")
            || x.EndsWith(".gif")
            || x.EndsWith(".cue")
            || x.EndsWith(".fpl")
            || x.EndsWith(".htm")
            || x.EndsWith(".pkf")
            || x.EndsWith(".db")
            || x.EndsWith(".webp")
            || x.EndsWith(".spotdl-cache")
            || x.EndsWith(".log")
            || x.EndsWith(".accurip")
        ));
    }

    public List<Song> GetQueueCopy()
    {
        return playerContext.Queue.ToList();
    }
}
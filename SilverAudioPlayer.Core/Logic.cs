using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using FuzzySharp;
using FuzzySharp.Extensions;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using Swordfish.NET.Collections;
using Swordfish.NET.Collections.Auxiliary;
using Logger = Serilog.Core.Logger;

namespace SilverAudioPlayer.Core;

public class PlayerContext : ReactiveObject
{
    private Song? _CurrentSong;
    public RepeatState _LoopType;
    public byte _Volume = 50;
    public Func<RepeatState>? GetLoopType;
    public Func<byte>? GetVolume;
    public Action? HandleLateStageMetadataAndScrollBar = null;
    public Action? ResetUIScrollBar = null;
    public Action<RepeatState>? SetLoopType;
    public Action<string, string>? ShowMessageBox;
    public Action<TimeSpan> SetScrollBarTextTo = null;
    public Action<byte>? VolumeChanged;
    public Func<IList<Song>>? GetQueue;
    public Action<IList<Song>>? SetQueue;

    public byte Volume
    {
        get => GetVolume();
        set => VolumeChanged(value);
    }

    public IList<Song> Queue
    {
        get => GetQueue();
        set => SetQueue(value);
    }

    public Song? CurrentSong
    {
        get => _CurrentSong;
        set
        {
            this.RaiseAndSetIfChanged(ref _CurrentSong, value); // Somehow does not work with ProcessFiles
            _CurrentSong = value;
        }
    }

    public RepeatState LoopType
    {
        get => GetLoopType();
        set => SetLoopType(value);
    }
}

public class Logic<T> where T : PlayerContext
{
    public readonly List<IMusicStatusInterface> musicStatusInterfaces = new();
    private bool ChangeAllowed = true;
    private bool IsSortRequested;
    public Song? NextSong;

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



    public void AddMSI(IMusicStatusInterface e)
    {
        musicStatusInterfaces.Add(e);
        e.Play += MusicStatusInterface_Play;
        e.Pause += MusicStatusInterface_Pause;
        e.Stop += MusicStatusInterface_Stop;
        e.PlayPause += MusicStatusInterface_PlayPause;
        e.Next += MusicStatusInterface_Next;
        e.Previous += MusicStatusInterface_Previous;
        e.GetCurrentTrack += MusicStatusInterface_GetCurrentTrack;
        e.GetDuration += MusicStatusInterface_GetDuration;
        e.GetPosition += MusicStatusInterface_GetPosition;
        e.SetPosition += MusicStatusInterface_SetPosition;
        e.GetShuffle += MusicStatusInterface_GetShuffle;
        e.GetState += MusicStatusInterface_GetState;
        e.GetVolume += MusicStatusInterface_GetVolume;
        e.SetVolume += MusicStatusInterface_SetVolume;
        e.GetRepeat += MusicStatusInterface_GetRepeat;
        e.SetRating += MusicStatusInterface_SetRating;
        e.SetRepeat += MusicStatusInterface_SetRepeat;
        e.StartIPC();
    }

    private void MusicStatusInterface_SetRepeat(object? sender, RepeatState e)
    {
        playerContext.LoopType = e;
    }

    private void MusicStatusInterface_SetVolume(object? sender, byte e)
    {
        if (e <= 100) Player?.SetVolume(e);
    }

    private void MusicStatusInterface_SetPosition(object? sender, ulong e)
    {
        Player?.SetPosition(TimeSpan.FromSeconds(e));
    }

    private void MusicStatusInterface_SetRating(object? sender, byte e)
    {
        //A music player should probably not edit the metadata of the music it plays
        //If someone thinks otherwise feel free to add code to this method
    }

    private bool MusicStatusInterface_GetShuffle()
    {
        return false;
    }

    private ulong MusicStatusInterface_GetPosition()
    {
        return (ulong)(Player?.GetPosition().TotalSeconds ?? 1);
    }

    private RepeatState MusicStatusInterface_GetRepeat()
    {
        return playerContext.LoopType;
    }

    private byte MusicStatusInterface_GetVolume()
    {
        return Volume;
    }

    private PlaybackState MusicStatusInterface_GetState()
    {
        return Player?.GetPlaybackState() ?? PlaybackState.Stopped;
    }

    private void MusicStatusInterface_Stop(object? sender, object e)
    {
        StopAutoLoading = true;
        Player?.Stop();
        SendIfStateIsNotNull();
    }

    private void MusicStatusInterface_Pause(object? sender, object e)
    {
        Player?.Pause();
        SendIfStateIsNotNull();
    }

    private void SendIfStateIsNotNull()
    {
        var state = Player?.GetPlaybackState();
        if (state != null)
            PlaybackStateChangedNotification(state.Value);
        else
            PlaybackStateChangedNotification(PlaybackState.Stopped);
    }

    /// <summary>
    ///     Lets music status interfaces know about a track change
    /// </summary>
    /// <param name="playerContext.CurrentSong">The new track</param>
    public void TrackChangedNotification(Song? currentSong)
    {
        Parallel.ForEach(musicStatusInterfaces, msI => msI?.TrackChangedNotification(currentSong!));
    }

    /// <summary>
    ///     Lets music status interfaces know about a playstate change
    /// </summary>
    /// <param name="s">The new Playstate</param>
    public void PlaybackStateChangedNotification(PlaybackState s)
    {
        Parallel.ForEach(musicStatusInterfaces, msI => msI?.PlayerStateChanged(s));
        if (s == PlaybackState.Stopped)
            Parallel.ForEach(WakeLockInterfaces, msI => msI.UnWakeLock());
        else if (s == PlaybackState.Playing) Parallel.ForEach(WakeLockInterfaces, msI => msI.WakeLock());
    }

    private void MusicStatusInterface_Play(object? sender, object e)
    {
        Play();
    }

    public void RemoveMSI(IMusicStatusInterface e)
    {
        e.Play -= MusicStatusInterface_Play;
        e.Pause -= MusicStatusInterface_Pause;
        e.Stop -= MusicStatusInterface_Stop;
        e.Next -= MusicStatusInterface_Next;
        e.Previous -= MusicStatusInterface_Previous;
        e.GetCurrentTrack -= MusicStatusInterface_GetCurrentTrack;
        e.GetDuration -= MusicStatusInterface_GetDuration;
        e.GetState -= MusicStatusInterface_GetState;
        e.GetVolume -= MusicStatusInterface_GetVolume;
        e.GetRepeat -= MusicStatusInterface_GetRepeat;
        e.GetPosition -= MusicStatusInterface_GetPosition;
        e.GetShuffle -= MusicStatusInterface_GetShuffle;
        e.SetRating -= MusicStatusInterface_SetRating;
        e.SetPosition -= MusicStatusInterface_SetPosition;
        e.SetVolume -= MusicStatusInterface_SetVolume;
        e.PlayPause -= MusicStatusInterface_PlayPause;
        e.SetRepeat -= MusicStatusInterface_SetRepeat;
        e.StopIPC();
        e.Dispose();
        musicStatusInterfaces.Remove(e);
    }

    public void PlayPause(bool allowstart)
    {
        if (Player?.GetPlaybackState() == PlaybackState.Playing)
            Pause();
        else if (Player?.GetPlaybackState() == PlaybackState.Paused)
            Play();
        else if (allowstart) Play();
    }

    private void MusicStatusInterface_PlayPause(object? sender, object e)
    {
        PlayPause(true);
    }

    private ulong MusicStatusInterface_GetDuration()
    {
        return (ulong?)Player?.Length()?.TotalSeconds ?? (ulong?)(playerContext.CurrentSong?.Metadata?.Duration / 1000) ?? 2;
    }

    private Song? MusicStatusInterface_GetCurrentTrack()
    {
        return playerContext.CurrentSong;
    }

    private void MusicStatusInterface_Previous(object? sender, EventArgs e)
    {
        Previous();
    }

    private void MusicStatusInterface_Next(object? sender, EventArgs e)
    {
        Next();
    }

    private void Next()
    {
        if (ChangeAllowed)
        {
            var a = playerContext.Queue.IndexOf(playerContext.CurrentSong);
            if (a != -1 && a + 1 < playerContext.Queue.Count)
            {
                ChangeAllowed = false;
                HandleSongChanging(playerContext.Queue[a + 1], true);
                ChangeAllowed = true;
            }
        }
    }

    private void Previous()
    {
        if (ChangeAllowed)
        {
            var a = playerContext.Queue.IndexOf(playerContext.CurrentSong);
            if (a != -1 && a - 1 >= 0)
            {
                ChangeAllowed = false;
                HandleSongChanging(playerContext.Queue[a - 1], true);
                ChangeAllowed = true;
            }
        }
    }

    public void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (MusicStatusInterfaces?.Any() == true)
            Parallel.ForEach(MusicStatusInterfaces, dangthing =>
            {
                var a = dangthing;
                GC.KeepAlive(a);
                AddMSI(a);
            });
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        StopAutoLoading = true;
        Parallel.ForEach(MusicStatusInterfaces.ToArray(), dangthing => RemoveMSI(dangthing));
        if (Player != null) Player.TrackEnd -= OutputDevice_PlaybackStopped;
        StopAutoLoading = true;
        Player?.Stop();
        Player = null;
        Environment.Exit(0);
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
            playerContext?.ShowMessageBox?.Invoke("Error", "I do not know how to play " + playerContext.CurrentSong.URI);
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

        if (resetsal) StopAutoLoading = false;
    }

    public void RemoveTrack()
    {
        Player?.Stop();
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

        StopAutoLoading = false;
    }

    public void HandleSongChanging(Song nextSong, bool resetsal = false)
    {
        Log.Information("StopAutoLoading set to true in HandleSongChanging");
        StopAutoLoading = true;
        playerContext.CurrentSong = nextSong;
        Debug.Assert(playerContext.CurrentSong == nextSong);
        RemoveTrack();
        StartPlaying(resetsal: resetsal);
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        if (files?.Any() == true)
        {
            var onefile = files.Count() == 1;
            if (onefile && Directory.Exists(files.First()))
            {
                files = Directory.GetFiles(files.First());
                onefile = false;
            }

            files = FilterFiles(files);
            foreach (var file in files) AddSong(new Song(file, file, Guid.NewGuid()), !onefile);
            if (!onefile && !IsSortRequested)
            {
                IsSortRequested = true;
                Task.Run(() => DoSort());
            }
        }
    }

    public void ProcessStreams(IEnumerable<WrappedStream> streams)
    {
        if (streams?.Any() == true)
        {
            var onefile = streams.Count() == 1;
            foreach (var file in streams) AddSong(new Song(file, "unknown", Guid.NewGuid()), true);
            if (!onefile && !IsSortRequested)
            {
                IsSortRequested = true;
                Task.Run(() => DoSort());
            }
        }
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
        Task.Run(async () =>
        {
            song.Metadata ??= await GetMetadataFromStream(song.Stream);
            if (song == null) throw new Exception();
            playerContext.Queue.Add(song);
            if (!expectmore && !IsSortRequested)
            {
                IsSortRequested = true;
                var sortTask = Task.Run(() => DoSort());
            }
        });
    }

    public async Task DoSort()
    {
        await Task.Delay(200);
        List<Song> sng = new();
        var albums = playerContext.Queue.ToList().GroupBy(a => a.Metadata.Album);
        List<Tuple<string?, List<Song>>> fuzzedAlbums = new();
        foreach (var album in albums)
            if (fuzzedAlbums.Find(x => Fuzz.Ratio(x.Item1 ?? "", album.Key ?? "") > 80) is Tuple<string?, List<Song>>
                group)
                group.Item2.AddRange(album.ToList());
            else
                fuzzedAlbums.Add(new Tuple<string?, List<Song>>(album.Key, album.ToList()));
        foreach (var album in fuzzedAlbums)
        {
            var discs = album.Item2.GroupBy(a => a?.Metadata?.DiscNumber ?? 0);
            foreach (var disc in discs.OrderBy(a => a.Key))
                sng.AddRange(disc.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
        }

        Log.Information("Sorted through {Count} songs", sng.Count);
        playerContext.Queue.Clear();
        playerContext.Queue.AddRange(sng);
        playerContext.Queue = playerContext.Queue;
        IsSortRequested = false;
    }

    public async Task<IPlay?> GetPlayerFromStream(WrappedStream stream)
    {
        return (await ChoosePlayProvider(PlayProviders?.Where(x => x.CanPlayFile(stream)), stream))?.GetPlayer(stream);
    }



    public async Task<Metadata?>? GetMetadataFromStream(WrappedStream stream)
    {
        return new MetadataCombo(MetadataProviders?.Where(x => x.CanGetMetadata(stream)).Select(async x => (await x.GetMetadata(stream))).Select(t => t.Result).Where(i => i != null).ToList());
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
            || x.EndsWith(".m3u")
            || x.EndsWith(".fpl")
            || x.EndsWith(".htm")
            || x.EndsWith(".pkf")
            || x.EndsWith(".db")
            || x.EndsWith(".webp")
            || x.EndsWith(".spotdl-cache")
        ));
    }

    public List<Song> GetQueueCopy()
    {
        return playerContext.Queue.ToList();
    }
}

public class MetadataCombo : Metadata
{
    public MetadataCombo()
    {

    }

    public IReadOnlyCollection<Metadata> OriginalMetadatas { get; }
    public MetadataCombo(IReadOnlyCollection<Metadata> metadatas)
    {
        OriginalMetadatas = metadatas;
        Title = metadatas.Select(x => x.Title).MaxN(1).First();
        Artist = metadatas.Select(x => x.Artist).MaxN(1).First();
        Album = metadatas.Select(x => x.Album).MaxN(1).First();
        Genre = metadatas.Select(x => x.Genre).MaxN(1).First();
        Year = ((int?)metadatas.Select(x => x.Year).Where(x => x is not 9999 or 0 or null).Average());
        TrackNumber = metadatas.Select(x => x.TrackNumber).MaxBy(x => x is not null);
        Duration = metadatas.Select(x => x.Duration).MaxBy(x => x is not null);
        Bitrate = metadatas.Select(x => x.Bitrate).MaxBy(x => x is not null);
        SampleRate = metadatas.Select(x => x.SampleRate).MaxBy(x => x is not null);
        Channels = metadatas.Select(x => x.Channels).MaxBy(x => x is not null);
        Pictures = metadatas.Select(x => x.Pictures).MaxBy(x => x?.Count);
        Lyrics = metadatas.Select(x => x.Lyrics).MaxBy(x => !string.IsNullOrEmpty(x));
        SyncedLyrics = metadatas.Select(x => x.SyncedLyrics).MaxBy(x => x.Count);
        if ((SyncedLyrics is null or { Count: 0 }) && !string.IsNullOrEmpty(Lyrics) && (Lyrics[0] == '['))
        {
            //There is a chance that the unsynced lyrics are actually synced, lets try and read them

            try
            {
                var s = global::Opportunity.LrcParser.Lyrics.Parse(Lyrics);
                SyncedLyrics = s.Lyrics.Lines.Select(x => new LyricPhrase((int)(x.Timestamp.Ticks / TimeSpan.TicksPerMillisecond), x.Content + "\n")).ToList();
            }
            catch { }
        }
        DiscNumber = metadatas.Select(x => x.DiscNumber).MaxBy(x => x is not null);
    }
}
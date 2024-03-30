using System.Composition;
using System.Diagnostics;
using System.Reactive.Linq;
using FuzzySharp;
using ReactiveUI;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;
using Swordfish.NET.Collections;
using Logger = Serilog.Core.Logger;

namespace SilverAudioPlayer.Core;

public interface ILogicImportable
{
    [ImportMany] public IEnumerable<IPlayProvider> PlayProviders { get; set; }

    [ImportMany] public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }

    [ImportMany] public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }

    [ImportMany] public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
    [ImportMany] public IEnumerable<IPlayStreamProvider> PlayStreamProviders { get; set; }
    [ImportMany] public IEnumerable<ISyncPlugin> SyncPlugins { get; set; }
    [ImportMany] public IEnumerable<IPlaylistReader> PlaylistReaders { get; set; }
    [ImportMany] public IEnumerable<IAdditionalMetadataGatherer> MetadataGatherers { get; set; }


    [Import] public IWillProvideMemory MemoryProvider { get; set; }
    public IEnumerable<ICodeInformation> PluginCodeInformation => [
            .. PlayProviders,
            .. MusicStatusInterfaces,
            .. MetadataProviders,
            .. WakeLockInterfaces,
            .. PlayStreamProviders,
            .. PlaylistReaders,
            .. SyncPlugins,
            .. MetadataGatherers
        ];


}
public class Logic<T> : ILogic, ILogicImportable where T : PlayerContext
{
    public readonly List<IMusicStatusInterface> LoadedMusicStatusInterfaces = [];
    private bool _changeAllowed = true;
    private bool _isSortRequested;
    public Song? NextSong;
    public Stack<Song> SongHistory = new(26);
    public bool StopAutoLoading;
    private readonly CancellationTokenSource? _token = new();

    public Logic(T playercontext)
    {
        PlayerContext = playercontext;
        PlayerContext.VolumeChanged ??= vol =>
        {
            Player?.SetVolume(vol);
            PlayerContext.RaiseAndSetIfChanged(ref PlayerContext._Volume, vol);
        };
        PlayerContext.GetVolume ??= () => PlayerContext._Volume;
        PlayerContext.GetLoopType ??= () => PlayerContext._LoopType;
        PlayerContext.SetLoopType ??= lt =>
        {
            PlayerContext.RaiseAndSetIfChanged(ref PlayerContext._LoopType, lt);
            if (Player != null && Player is ILoop loop)
            {
                if (PlayerContext._LoopType == RepeatState.One)
                {
                    loop.EnableLoop();
                }
                else
                {
                    loop.DisableLoop();
                }
            }
        };
        ConcurrentObservableCollection<Song> queue = [];
        playercontext.GetQueue ??= () => queue;

        ChoosePlayProvider ??= (x, _) => Task.FromResult(x.FirstOrDefault());
    }

    public Action<Action> DoQueueManipulationAction = (a) => { Task.Run(a); };

    [ImportMany] public IEnumerable<IPlayProvider> PlayProviders { get; set; }

    [ImportMany] public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }

    [ImportMany] public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }

    [ImportMany] public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
    [ImportMany] public IEnumerable<IPlayStreamProvider> PlayStreamProviders { get; set; }
    [ImportMany] public IEnumerable<ISyncPlugin> SyncPlugins { get; set; }
    [ImportMany] public IEnumerable<IPlaylistReader> PlaylistReaders { get; set; }
    [ImportMany] public IEnumerable<IAdditionalMetadataGatherer> MetadataGatherers { get; set; }

    [Import] public IWillProvideMemory MemoryProvider { get; set; }


    public Func<IEnumerable<IPlayProvider>, WrappedStream, Task<IPlayProvider?>> ChoosePlayProvider { get; set; }

    public Logger Log { get; set; }
    public T PlayerContext { get; set; }
    public List<MimeType> PlayableMimes { get; set; }
    public IFullPlayerEnvironment PlayerEnvironment { get; set; }
    private byte Volume
    {
        get => PlayerContext.Volume;
        set => PlayerContext.Volume = value;
    }

    public IPlay? Player { get; set; }


    IMusicStatusInterfaceListenerAdmin _musicStatusInterface;

    public void AddMSI(IMusicStatusInterface statusInterface, IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
    {
        LoadedMusicStatusInterfaces.Add(statusInterface);
        _musicStatusInterface = logicMusicInterfaceListener;
        statusInterface.StartIPC(logicMusicInterfaceListener);
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
        _musicStatusInterface?.FireTrackChangedNotification(currentSong!);
    }

    /// <summary>
    ///     Lets music status interfaces know about a playstate change
    /// </summary>
    /// <param name="s">The new Playstate</param>
    public void PlaybackStateChangedNotification(PlaybackState s)
    {
        _musicStatusInterface?.FirePlayerStateChanged(s);
        switch (s)
        {
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                Parallel.ForEach(WakeLockInterfaces, msI => msI.UnWakeLock());
                break;
            case PlaybackState.Playing:
                Parallel.ForEach(WakeLockInterfaces, msI => msI.WakeLock());
                break;
            case PlaybackState.Buffering:
            default:
                break;
        }
    }


    public void RemoveMsi(IMusicStatusInterface statusInterface, IMusicStatusInterfaceListener logicMusicInterfaceListener)
    {
        statusInterface.StopIPC(logicMusicInterfaceListener);
        statusInterface.Dispose();
        LoadedMusicStatusInterfaces.Remove(statusInterface);
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
        if (!_changeAllowed || PlayerContext.CurrentSong == null) return;
        var a = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
        if (a == -1 || a + 1 >= PlayerContext.Queue.Count) return;
        _changeAllowed = false;
        HandleSongChanging(PlayerContext.Queue[a + 1], true);
        _changeAllowed = true;
    }

    public void Previous()
    {
        if (!_changeAllowed || PlayerContext.CurrentSong == null) return;
        var a = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
        if (a == -1 || a - 1 < 0) return;
        _changeAllowed = false;
        HandleSongChanging(PlayerContext.Queue[a - 1], true);
        _changeAllowed = true;
    }
    public void SetPosition(double seconds) => Player?.SetPosition(TimeSpan.FromSeconds(seconds));
    public void SetPosition(TimeSpan pos) => Player?.SetPosition(pos);

    public IPlaylist? TryReadPlaylist(WrappedStream playlistStream)
    {
        var playlistReader = PlaylistReaders.FirstOrDefault(x => x.SupportedMimes.Contains(playlistStream.MimeType));
        return playlistReader != null && playlistReader.TryParse(playlistStream, out var playlist) ? playlist : null;
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
        if (Player != null && Player.IsTrackLoaded)
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
        if (PlayerContext.CurrentSong == null)
        {
            if (PlayerContext.Queue.Count > 0)
            {
                PlayerContext.CurrentSong = PlayerContext.Queue[0];
                if (PlayerContext.Queue.Count >= 2)
                {
                    NextSong = PlayerContext.Queue[0];
                }
            }
            else
            {
                Log.Information("Avoiding fatal crash, nothing to play");
                return;
            }
        }
        Player?.Stop();
        bool newPlayer = true;
        if (Player is IPlayStreams p && p.SupportedMimes?.Contains(PlayerContext.CurrentSong.Stream.MimeType) == true)
        {
            //Worth a shot?
            newPlayer = false;
            Log.Debug("Using old player");
            p.LoadStream(PlayerContext.CurrentSong.Stream);
        }
        else
        {
            Log.Debug("disposing old player and requesting new one");

            Player?.Dispose();
            try
            {
                Player = await GetPlayerFromStream(PlayerContext.CurrentSong.Stream);
            }
            catch (Exception e)
            {
                //uhhhhhh
                Log.Error(e, "EXCEPTION during startplaying");
                Player?.Dispose();
                Player = null;
            }

        }

        if (Player == null)
        {
            PlayerContext?.ShowMessageBox?.Invoke("Error",
                "I do not know how to play " + PlayerContext.CurrentSong.URI);
            return;
        }
        Serilog.Log.Information("Got player of type {PlayerType} New: {newPlayer}", Player.GetType(), newPlayer);
        _ = Task.Run(() => TrackChangedNotification(PlayerContext.CurrentSong));
        if (play)
        {
            Player.SetVolume(Volume);
            Player.Play();
            var informPlayerOfLoop = Task.Run(async () =>
            {
                await Task.Delay(600);
                if (Player != null && Player is ILoop loop)
                {
                    if (PlayerContext.LoopType == RepeatState.One)
                    {
                        loop.EnableLoop();
                    }
                    else
                    {
                        loop.DisableLoop();
                    }
                }
            });

            SendIfStateIsNotNull();
            if (newPlayer)
            {
                Player.TrackEnd += OutputDevice_PlaybackStopped;
            }
            PlayerContext?.ResetUIScrollBar?.Invoke();
            if (Player?.Length() is { } total) PlayerContext?.SetScrollBarTextTo?.Invoke(total);
            PlayerContext?.HandleLateStageMetadataAndScrollBar?.Invoke();
        }

        if (!resetsal) return;
        Serilog.Log.Information("StopAutoLoading set to false in StartPlaying");
        StopAutoLoading = false;
    }

    public void RemoveTrack()
    {
        Player?.Stop();
        SendIfStateIsNotNull();
        _token?.Cancel();
        Thread.Sleep(30);
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        Serilog.Log.Information(@"Output device playback stopped
StopAutoLoading: {StopAutoLoading}
Current song: {CurrentSong}
Loop mode: {LoopType}", StopAutoLoading, PlayerContext.CurrentSong, PlayerContext.LoopType);
        if (PlayerContext.LoopType == RepeatState.One && !StopAutoLoading)
        {
            //Loop
            StartPlaying();
        }
        else if (PlayerContext.CurrentSong != null && !StopAutoLoading)
        {
            var a = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            if (a != -1)
            {
                if (a + 1 < PlayerContext.Queue.Count)
                {
                    HandleSongChanging(PlayerContext.Queue[a + 1], true);
                }
                else if (PlayerContext.LoopType == RepeatState.Queue)
                {
                    HandleSongChanging(PlayerContext.Queue[0], true);
                }
                else
                {
                    Serilog.Log.Information("RemoveTrack in PlaybackStopped");
                    RemoveTrack();
                }
            }
            else if (NextSong != null)
            {
                PlayerContext.CurrentSong?.Dispose();
                HandleSongChanging(NextSong, true);
                NextSong = null;
            }
            else
            {
                PlayerContext.CurrentSong?.Dispose();
                PlayerContext.CurrentSong = null;
            }
        }
        Serilog.Log.Information("StopAutoLoading set to false in OutputDevice_PlaybackStopped");
        StopAutoLoading = false;
    }

    public void HandleSongChanging(Song nextSong, bool resetsal = false)
    {
        Serilog.Log.Information("StopAutoLoading set to true in HandleSongChanging");
        StopAutoLoading = true;
        if (PlayerContext.CurrentSong is not null)
        {
            SongHistory.Push(PlayerContext.CurrentSong);
        }
        PlayerContext.CurrentSong = nextSong;
        Debug.Assert(PlayerContext.CurrentSong.Equals(nextSong));
        RemoveTrack();
        StartPlaying(resetsal: resetsal);
    }

    void ProcessFile(string file, bool oneFile)
    {
        if (file.EndsWith(".m3u") || file.EndsWith(".m3u8") || file.EndsWith(".spl.br"))
        {
            var playlist = TryReadPlaylist(file.StartsWith("http") ? new WrappedHttpStream(file) : new WrappedFileStream(file));
            if (playlist == null) return;
            foreach (var song in playlist.Tracks)
            {
                foreach (var source in song.TrackLoadInfos)
                {
                    AddWithURLBad(source.URL, source.Source == "http", false);
                    break;
                }
            }
        }

        else
        {
            AddWithURLBad(file, file.StartsWith("http"), oneFile);
        }

    }

    public void AddWithURLBad(string f, bool http, bool oneFile)
    {
        var psps = PlayStreamProviders.Where(x =>
            x is IPlayStreamProviderThatSupportsUrls y && y.IsUrlSupported(new(f), PlayerEnvironment));
        if (psps.Any())
        {
            Task.Run(async () =>
                await ((IPlayStreamProviderThatSupportsUrls)psps.First()).LoadUrlAsync(new(f), PlayerEnvironment));
            return;
        }
        else
        {
            if (!http)
            {
                if (File.Exists(f))
                {
                    AddSong(new Song(new WrappedFileStream(f), f, logic: this), !oneFile);
                }
            }
            else
            {
                AddSong(new Song(new WrappedHttpStream(f), f, logic: this), !oneFile);
            }
        }
    }
    public void ProcessFiles(IEnumerable<string> files)
    {
        var enumerable = files.ToList();
        if (enumerable?.Count == 0 || enumerable == null) return;
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
                    ProcessFile(file, oneFile);
                }
            }
            else
            {
                ProcessFile(path, oneFile);
            }
        }
        SuggestDoingSort();
    }

    public void ProcessStreams(IEnumerable<WrappedStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Count == 0) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var file in wrappedStreams) AddSong(new Song(file, "unknown", logic: this), true);
        if (oneFile) return;
        SuggestDoingSort();
    }

    public void ProcessStream(WrappedStream stream)
    {
        AddSong(new Song(stream, "unknown", logic: this));
    }

    public void ClearAll()
    {
        var delList = PlayerContext.Queue.ToList();
        foreach (var track in delList)
        {
            if (!ReferenceEquals(PlayerContext.CurrentSong, track))
            {
                track.Dispose();
            }

            PlayerContext.Queue.Remove(track);
        }
    }

    public void AddSong(Song song, bool expectmore = false)
    {
        PlayerContext.Queue.Add(song);
        if (expectmore) return;
        SuggestDoingSort();
    }

    public void DoneAddingSongs()
    {
        SuggestDoingSort();
    }
    void SuggestDoingSort()
    {
        if (_isSortRequested) return;
        _isSortRequested = true;
        var sortTask = Task.Run(DoSort);
    }
    public async Task DoSort()
    {
        await Task.Delay(200);
        List<Song> sng = [];
        var albums = PlayerContext.Queue.ToList().GroupBy(a => a.Metadata?.Album);
        List<Tuple<string?, List<Song>>> fuzzedAlbums = [];
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

        Serilog.Log.Information("Sorted through {Count} songs", sng.Count);
        DoQueueManipulationAction.Invoke(() =>
        {
            PlayerContext.Queue.Clear();
            foreach (var song in sng)
            {
                PlayerContext.Queue.Add(song);
            }
        });
        _isSortRequested = false;
    }

    public async Task<IPlay?> GetPlayerFromStream(WrappedStream stream)
    {
        return (await ChoosePlayProvider(PlayProviders.Where(x => x.CanPlayFile(stream)), stream))?.GetPlayer(stream, PlayerEnvironment);
    }


    public Task<IMetadata?>? GetMetadataFromStream(WrappedStream stream, IEnumerable<IMetadata>? additionalMetadata = null)
    {
        var listofmetadatas = MetadataProviders?.Where(x => x.CanGetMetadata(stream))
            .Select(async x => await x.GetMetadata(stream)).Select(t => t.Result).Where(i => i != null).ToList();
        if (additionalMetadata != null)
        {
            listofmetadatas.AddRange(additionalMetadata);
        }
        return Task.FromResult<IMetadata?>(new MetadataCombo(listofmetadatas, MetadataGatherers));
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
            || x.EndsWith(".lrc")
            || x.EndsWith(".accurip")
        ));
    }

    public List<Song> GetQueueCopy()
    {
        return [.. PlayerContext.Queue];
    }
    private static Tuple<List<string>, List<string>> SupportedTrackMimesG(IEnumerable<MimeType> supportedMimes)
    {
        var mimeTypes = supportedMimes.Select(x => x.Common).ToList();
        mimeTypes.AddRange(supportedMimes.SelectMany(y => y.AlternativeTypes));
        var fileExt = supportedMimes.SelectMany(x => x.FileExtensions).ToList();
        return new(mimeTypes, fileExt);
    }
    public Tuple<List<string>, List<string>> SupportedTrackMimes()
    {
        return Logic<T>.SupportedTrackMimesG(PlayableMimes);
    }

    public Tuple<List<string>, List<string>> SupportedPlaylistMimes()
    {
        var playlistMimes = PlaylistReaders.SelectMany(x => x.SupportedMimes).Distinct();
        return Logic<T>.SupportedTrackMimesG(playlistMimes);
    }
    public Tuple<List<string>, List<string>> SupportedTrackAndPlaylistMimes()
    {
        var (a, b) = Logic<T>.SupportedTrackMimesG(PlayableMimes);
        var (c, d) = SupportedPlaylistMimes();
        a.AddRange(c);
        b.AddRange(d);
        return new(a, b);
    }


}


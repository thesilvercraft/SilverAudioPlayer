using System.Composition;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Threading;
using FuzzySharp;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverAudioPlayer.Shared.StandardRequests;
using SilverMagicBytes;
using Swordfish.NET.Collections;

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

    public IEnumerable<ICodeInformation> PluginCodeInformation =>
    [
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
    public Song? NextSong;
    public bool StopAutoLoading;
    private IMediator _mediator;
    public bool PreferAlbumReplayGain = false;

    public Logic(T playercontext)
    {
        PlayerContext = playercontext ?? throw new ArgumentNullException(nameof(playercontext));
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
            if (Player is not ILoop loop) return;
            if (PlayerContext._LoopType == RepeatState.One)
            {
                loop.EnableLoop();
            }
            else
            {
                loop.DisableLoop();
            }
        };
        ConcurrentObservableCollection<Song> queue = [];
        PlayerContext.GetQueue ??= () => queue;
        ChoosePlayProvider ??= (providers, _) => Task.FromResult(providers.FirstOrDefault());
        _mediator = PlayerContext.ServiceProvider.GetRequiredService<IMediator>();
        _mediator.OnQueryReceived += MediatorQuery;
    }

    void MediatorQuery(QueryEventArgs query)
    {
        if (query.Query is SilverAudioPlayer.Shared.StandardRequests.RequestReplayGainInfo)
        {
            if (PlayerContext.CurrentSong is not { Metadata: { Entries.Count: > 0 } metadata } s) return;
            var e = metadata.Entries;

            float? TryGetReplayGainPeak(bool isAlbum)
            {
                if (e.TryGetValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_PEAK", out var al) &&
                    float.TryParse(al, CultureInfo.InvariantCulture, out var r))
                {
                    return r;
                }
                return null;
            }

            bool TryGetReplayGain(bool isAlbum, out float gain)
            {
                gain = 0;
                if (e.TryGetValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_GAIN", out var val))
                {
                    var cleanVal = val.Replace("dB", "", StringComparison.OrdinalIgnoreCase).Trim();
                    return float.TryParse(cleanVal, CultureInfo.InvariantCulture, out gain);
                }
                return false;
            }

            bool primaryIsAlbum = PreferAlbumReplayGain;
            bool secondaryIsAlbum = !PreferAlbumReplayGain;

            bool album = primaryIsAlbum;
            bool rgSuccess = TryGetReplayGain(primaryIsAlbum, out float r);

            if (!rgSuccess)
            {
                album = secondaryIsAlbum;
                rgSuccess = TryGetReplayGain(secondaryIsAlbum, out r);
            }

            float? p = TryGetReplayGainPeak(album);

            query.Handled = true;
            query.Result = new ReplayGainInfo()
            {
                Album = album,
                Gain = r,
                Peak = p,
                Success = rgSuccess
            };
        }
       /* else if (query.Query is Config)
        {
            
        }*/
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

    [Import] public IWillProvideMemory? MemoryProvider { get; set; }


    public Func<IEnumerable<IPlayProvider>, WrappedStream, Task<IPlayProvider?>> ChoosePlayProvider { get; set; }

    public ILogger Log { get; set; }
    public T PlayerContext { get; set; }
    public List<MimeType> PlayableMimes { get; set; }
    public IFullPlayerEnvironment PlayerEnvironment { get; set; }

    private byte Volume
    {
        get => PlayerContext.Volume;
        set => PlayerContext.Volume = value;
    }

    /// <summary>
    /// This Logic's current player
    /// Avoid direct interactions unless necessary 
    /// </summary>
    public IPlay? Player { get; set; }


    IMusicStatusInterfaceListenerAdmin _musicStatusInterface;

    public void AddMSI(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
    {
        LoadedMusicStatusInterfaces.Add(statusInterface);
        _musicStatusInterface = logicMusicInterfaceListener;
        statusInterface.StartIPC(logicMusicInterfaceListener);
    }


    private void SendIfStateIsNotNull()
    {
        var state = Player?.GetPlaybackState();
        PlaybackStateChangedNotification(state ?? PlaybackState.Stopped);
    }

    /// <summary>
    ///     Lets music status interfaces know about a track change
    /// </summary>
    /// <param name="currentSong">The new track</param>
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

        Task.Run(() =>
        {
            switch (s)
            {
                case PlaybackState.Playing:
                    Parallel.ForEach(WakeLockInterfaces, provider => provider.WakeLock());
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    Parallel.ForEach(WakeLockInterfaces, provider => provider.UnWakeLock());
                    break;
            }
        });
    }


    public void RemoveMsi(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListener logicMusicInterfaceListener)
    {
        statusInterface.StopIPC(logicMusicInterfaceListener);
        statusInterface.Dispose();
        LoadedMusicStatusInterfaces.Remove(statusInterface);
    }

    public void PlayPause(bool allowStart)
    {
        if (Player?.GetPlaybackState() != PlaybackState.Playing)
        {
            if (Player?.GetPlaybackState() == PlaybackState.Paused || allowStart) Play();
        }
        else
        {
            Pause();
        }
    }


    public void Next()
    {
        if (!_changeAllowed || PlayerContext.CurrentSong == null) return;
        lock (PlayerContext.Queue)
        {
            var a = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            if (a == -1 || a + 1 >= PlayerContext.Queue.Count) return;
            _changeAllowed = false;
            HandleSongChanging(PlayerContext.Queue[a + 1]);
            _changeAllowed = true;
        }
    }

    public void Previous()
    {
        if (!_changeAllowed || PlayerContext.CurrentSong == null) return;
        lock (PlayerContext.Queue)
        {
            var a = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            if (a == -1 || a - 1 < 0) return;
            _changeAllowed = false;
            HandleSongChanging(PlayerContext.Queue[a - 1]);
            _changeAllowed = true;
        }
    }

    public void SetPosition(double seconds) => SetPosition(TimeSpan.FromSeconds(seconds));

    public void SetPosition(TimeSpan pos) => Player?.SetPosition(pos);

    public IPlaylist? TryReadPlaylist(WrappedStream playlistStream)
    {
        var playlistReader = PlaylistReaders.FirstOrDefault(x => x.SupportedMimes.Contains(playlistStream.MimeType));
        return playlistReader != null && playlistReader.TryParse(playlistStream, out var playlist) ? playlist : null;
    }

    public Func<string, bool> CheckIfPluginAllowed { get; set; } = _ => true;

    public void MainWindow_Opened(IMusicStatusInterfaceListenerAdmin musicStatusInterface)
    {
        if (MusicStatusInterfaces?.Any() == true)
            Task.Run(() =>
            {
                Parallel.ForEach(MusicStatusInterfaces, dangthing =>
                {
                    Debug.WriteLine("mainwindowload loading " + dangthing.GetType().FullName);
                    var a = dangthing;
                    if (!CheckIfPluginAllowed(a.GetType().FullName)) return;
                    GC.KeepAlive(a);
                    AddMSI(a, musicStatusInterface);
                    Debug.WriteLine("mainwindowload loaded " + dangthing.GetType().FullName);
                });
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

    private Song? GetNextSongInQueue()
    {
        if (PlayerContext.CurrentSong == null) return null;
        lock (PlayerContext.Queue)
        {
            var index = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            if (index < 0 || index + 1 >= PlayerContext.Queue.Count) return null;
            return PlayerContext.Queue[index + 1];
        }
    }

    private void UpdateNextSongReference()
    {
        if (PlayerContext.CurrentSong == null)
        {
            NextSong = null;
            return;
        }

        lock (PlayerContext.Queue)
        {
            var index = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            NextSong = index >= 0 && index + 1 < PlayerContext.Queue.Count
                ? PlayerContext.Queue[index + 1]
                : null;
        }
    }

    private void TryEnqueueNextSongForGaplessPlayback()
    {
        if (Player is not IPlayLancer lancer) return;
        if (PlayerContext.CurrentSong == null) return;

        var nextSong = GetNextSongInQueue();
        if (nextSong == null) return;

        if (NextSong != null && ReferenceEquals(nextSong, NextSong))
        {
            return;
        }

        try
        {
            if (lancer.EnqueueNextStream(nextSong.Stream))
            {
                NextSong = nextSong;
                Log.Debug("Enqueued next stream for gapless playback after add: {NextSong}", nextSong.URI);
            }
            else
            {
                Log.Debug("Player refused to enqueue next stream after add: {NextSong}", nextSong.URI);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enqueue next stream after add: {NextSong}", nextSong.URI);
        }
    }

    //TODO SEE IF CAN BE REPLACED WITH TASK
    public void StartPlaying(bool play = true, bool resetsal = false)
    {
        StartPlayingAsync(play, resetsal).GetAwaiter().GetResult();
    }

    public async Task StartPlayingAsync(bool play = true, bool resetsal = false)
    {
        if (PlayerContext.CurrentSong == null)
        {
            if (PlayerContext.Queue.Count > 0)
            {
                PlayerContext.CurrentSong = PlayerContext.Queue[0];
                if (PlayerContext.Queue.Count >= 2)
                {
                    NextSong = PlayerContext.Queue[1];
                }
            }
            else
            {
                Log.Information("Avoiding fatal crash, nothing to play");
                return;
            }
        }

        Player?.Stop();
        bool newPlayerRequired = true;
        if (Player is IPlayStreams p && p.SupportedMimes?.Contains(PlayerContext.CurrentSong.Stream.MimeType) == true)
        {
            //Worth a shot?
            newPlayerRequired = false;
            Log.Debug("Using old player");
            p.LoadStream(PlayerContext.CurrentSong.Stream);
        }
        else
        {
            Log.Debug("Requesting new player ");
            if (Player != null)
            {
                Player.PositionChanged -= OnPositionChanged;
                if (Player is IPlayLancer oldLancer)
                {
                    oldLancer.NextStreamStarted -= OnNextStreamStarted;
                }
                Player.Dispose();
            }

            try
            {
                Player = await GetPlayerFromStream(PlayerContext.CurrentSong.Stream);
                if (Player != null)
                {
                    Player.PositionChanged += OnPositionChanged;
                    if (Player is IPlayLancer newLancer)
                    {
                        newLancer.NextStreamStarted += OnNextStreamStarted;
                    }
                }
            }
            catch (Exception e)
            {
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

        Serilog.Log.Information("Got player of type {PlayerType} New: {newPlayer}", Player.GetType(),
            newPlayerRequired);
        TrackChangedNotification(PlayerContext.CurrentSong);
        UpdateNextSongReference();
        if (Player is IPlayLancer lancer)
        {
            lancer.NextStreamStarted -= OnNextStreamStarted;
            lancer.NextStreamStarted += OnNextStreamStarted;

            var nextSong = GetNextSongInQueue();
            if (nextSong != null)
            {
                try
                {
                    if (lancer.EnqueueNextStream(nextSong.Stream))
                    {
                        Log.Debug("Enqueued next stream for gapless playback: {NextSong}", nextSong.URI);
                    }
                    else
                    {
                        Log.Debug("Player refused to enqueue next stream: {NextSong}", nextSong.URI);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to enqueue next stream for {NextSong}", nextSong.URI);
                }
            }
        }

        if (play)
        {
            Player.SetVolume(Volume);
            Player.Play();
            var informPlayerOfLoop = Task.Run(async () =>
            {
                await Task.Delay(600);
                if (Player is ILoop loop)
                {
                    if (PlayerContext.LoopType == RepeatState.One)
                        loop.EnableLoop();
                    else
                        loop.DisableLoop();
                }
            });

            SendIfStateIsNotNull();
            if (newPlayerRequired)
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

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        _musicStatusInterface?.FirePositionChanged(e);
    }

    private void OnNextStreamStarted(object? sender, EventArgs e)
    {
        var promotedSong = GetNextSongInQueue();
        if (promotedSong == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            PlayerContext.CurrentSong = promotedSong;
            UpdateNextSongReference();
            TrackChangedNotification(promotedSong);
            PlayerContext?.ResetUIScrollBar?.Invoke();
            if (Player?.Length() is { } total)
            {
                PlayerContext?.SetScrollBarTextTo?.Invoke(total);
            }
            PlayerContext?.HandleLateStageMetadataAndScrollBar?.Invoke();

            var followingSong = GetNextSongInQueue();
            if (followingSong != null && Player is IPlayLancer lancer)
            {
                try
                {
                    if (lancer.EnqueueNextStream(followingSong.Stream))
                    {
                        Log.Debug("Enqueued following stream for gapless playback: {NextSong}", followingSong.URI);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to enqueue following stream for {NextSong}", followingSong.URI);
                }
            }
        });
    }

    public void RemoveTrack()
    {
        Player?.Stop();
        SendIfStateIsNotNull();
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        Serilog.Log.Information("""
                                Output device playback stopped
                                StopAutoLoading: {StopAutoLoading}
                                Current song: {CurrentSong}
                                Loop mode: {LoopType}
                                """, StopAutoLoading, PlayerContext.CurrentSong, PlayerContext.LoopType);
        if (PlayerContext.LoopType == RepeatState.One && !StopAutoLoading)
        {
            //Loop
            StartPlaying();
        }
        else if (PlayerContext.CurrentSong != null && !StopAutoLoading)
        {
            var currentTrackIndex = PlayerContext.Queue.IndexOf(PlayerContext.CurrentSong);
            if (currentTrackIndex != -1)
            {
                if (currentTrackIndex + 1 < PlayerContext.Queue.Count)
                {
                    HandleSongChanging(PlayerContext.Queue[currentTrackIndex + 1]);
                }
                else if (PlayerContext.LoopType == RepeatState.Queue)
                {
                    HandleSongChanging(PlayerContext.Queue[0]);
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
                HandleSongChanging(NextSong);
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

    public void HandleSongChanging(Song nextSong)
    {
        Serilog.Log.Information("StopAutoLoading set to true in HandleSongChanging");
        StopAutoLoading = true;
        PlayerContext.CurrentSong = nextSong;
        Debug.Assert(PlayerContext.CurrentSong.Equals(nextSong));
        RemoveTrack();
        StartPlaying(resetsal: true);
    }

    void ProcessFile(string file, bool oneFile)
    {
        if (SupportedPlaylistMimes().Item2.Any(file.EndsWith))
        {
            var playlist = TryReadPlaylist(file.StartsWith("http")
                ? new WrappedHttpStream(file, PlayerContext.ServiceProvider.GetRequiredService<HttpClient>())
                : new WrappedFileStream(file));
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
                AddSong(
                    new Song(new WrappedHttpStream(f, PlayerContext.ServiceProvider.GetRequiredService<HttpClient>()),
                        f, logic: this), !oneFile);
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

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Count == 0) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var file in wrappedStreams) AddSong(new Song(file, "unknown", logic: this), true);
        if (oneFile) return;
        SuggestDoingSort();
    }

    public void LoadSong(WrappedStream stream)
    {
        LoadSong(stream, null);
    }

    public void LoadSong(WrappedStream stream, IMetadata? additionalMetadata)
    {
        AddSong(additionalMetadata != null
            ? new Song(stream, "unknown", logic: this, metadata: additionalMetadata)
            : new Song(stream, "unknown", logic: this));
    }

    public void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Count == 0) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var (file, meta) in wrappedStreams)
        {
            AddSong(meta != null
                ? new Song(file, "unknown", logic: this, metadata: meta)
                : new Song(file, "unknown", logic: this), true);
        }

        if (oneFile) return;
        SuggestDoingSort();
    }

    public void ClearAll()
    {
        lock (PlayerContext.Queue)
        {
            var tracksToRemove = PlayerContext.Queue
                .Where(track => !ReferenceEquals(PlayerContext.CurrentSong, track))
                .ToList();

            DoQueueManipulationAction.Invoke(() =>
            {
                lock (PlayerContext.Queue)
                {
                    foreach (var track in tracksToRemove)
                    {
                        PlayerContext.Queue.Remove(track);
                        track.Dispose();
                    }
                }

                Log.Information("Queue cleared. Preserved currently playing track.");
            });
        }
    }

    public void AddSong(Song song, bool expectmore = false)
    {
        PlayerContext.Queue.Add(song);
        if (expectmore) return;
        SuggestDoingSort();

        if (PlayerContext.CurrentSong != null && Player is IPlayLancer)
        {
            TryEnqueueNextSongForGaplessPlayback();
        }
    }

    public void ReorderQueue(int sourceIndex, int targetIndex)
    {
        lock (PlayerContext.Queue)
        {
            if (sourceIndex < 0 || sourceIndex >= PlayerContext.Queue.Count) return;
            targetIndex = Math.Clamp(targetIndex, 0, PlayerContext.Queue.Count);
            if (sourceIndex == targetIndex || sourceIndex + 1 == targetIndex) return;

            var song = PlayerContext.Queue[sourceIndex];
            PlayerContext.Queue.RemoveAt(sourceIndex);
            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }
            PlayerContext.Queue.Insert(targetIndex, song);

            if (PlayerContext.CurrentSong != null)
            {
                var oldNext = NextSong;
                UpdateNextSongReference();
                if (Player is IPlayLancer && !ReferenceEquals(oldNext, NextSong))
                {
                    if (NextSong != null && ReplaceNextStreamForGaplessPlayback(NextSong))
                    {
                        Log.Debug("Replaced queued gapless stream after reorder: {NextSong}", NextSong.URI);
                    }
                    else
                    {
                        TryEnqueueNextSongForGaplessPlayback();
                    }
                }
            }
        }
    }

    private bool ReplaceNextStreamForGaplessPlayback(Song nextSong)
    {
        if (nextSong == null) return false;
        if (Player is not IPlayLancer lancer) return false;
        try
        {
            return lancer.ReplaceNextStream(nextSong.Stream);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to replace next stream for gapless playback");
            return false;
        }
    }

    public void RemoveSong(Song song)
    {
        ArgumentNullException.ThrowIfNull(song, nameof(song));
        var index = PlayerContext.Queue.IndexOf(song);
        if (index == -1) return;

        // 1. Handle NextSong reference shifting
        if (song.Equals(NextSong))
        {
            NextSong = (index + 1 < PlayerContext.Queue.Count) ? PlayerContext.Queue[index + 1] : null;
        }

        // 2. Safely teardown if it's the active track
        if (PlayerContext.CurrentSong?.Equals(song) == true)
        {
            Log.Information("Removing the currently playing track. Halting engine audio pipeline first.");

            // Stops the player backend and breaks the stream hook
            RemoveTrack();

            // Null out the reference so UI/Context let go of the instance
            PlayerContext.CurrentSong = null;

            // Now it is safe to tear down the HTTP connection / buffers
            song.Dispose();
        }
        else
        {
            // If it's just sitting in the queue, it's safe to dispose immediately
            song.Dispose();
        }

        PlayerContext.Queue.RemoveAt(index);
    }

    public void RemoveSongs(IReadOnlyList<Song?> selectedItems)
    {
        foreach (var song in selectedItems.ToList())
        {
            RemoveSong(song);
        }
    }

    private readonly object _sortLock = new();
    private CancellationTokenSource? _sortCancellationTokenSource;

    private void SuggestDoingSort()
    {
        lock (_sortLock)
        {
            _sortCancellationTokenSource?.Cancel();
            _sortCancellationTokenSource = new CancellationTokenSource();

            var token = _sortCancellationTokenSource.Token;
            Task.Run(async () => await DoSortAsync(token), token);
        }
    }

    public async Task DoSortAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            List<Song> elementsCopy;
            lock (PlayerContext.Queue)
            {
                elementsCopy = PlayerContext.Queue.ToList();
            }

            var albums = elementsCopy.GroupBy(a => a.Metadata?.Album);
            List<Tuple<string?, List<Song>>> fuzzedAlbums = [];

            foreach (var album in albums)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var matchedGroup = fuzzedAlbums.Find(x => Fuzz.Ratio(x.Item1 ?? "", album.Key ?? "") > 80);
                if (matchedGroup != null)
                    matchedGroup.Item2.AddRange(album.ToList());
                else
                    fuzzedAlbums.Add(new Tuple<string?, List<Song>>(album.Key, album.ToList()));
            }

            List<Song> sortedSongs = [];
            foreach (var disc in fuzzedAlbums.Select(album => album.Item2.GroupBy(a => a?.Metadata?.DiscNumber ?? 0))
                         .SelectMany(discs => discs.OrderBy(a => a.Key)))
            {
                if (cancellationToken.IsCancellationRequested) return;
                sortedSongs.AddRange(disc.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
            }

            if (cancellationToken.IsCancellationRequested) return;

            DoQueueManipulationAction.Invoke(() =>
            {
                lock (PlayerContext.Queue)
                {
                    PlayerContext.Queue.Clear();
                    foreach (var song in sortedSongs)
                    {
                        PlayerContext.Queue.Add(song);
                    }
                }

                Log.Information("Sorted through {Count} songs dynamically.", sortedSongs.Count);
                TryEnqueueNextSongForGaplessPlayback();
            });
        }
        catch (TaskCanceledException)
        {
            // Expected bubble cleanup trace handler
        }
    }

    public async Task<IPlay?> GetPlayerFromStream(WrappedStream stream)
    {
        return (await ChoosePlayProvider(PlayProviders.Where(x => x.CanPlayFile(stream)), stream))?.GetPlayer(stream,
            PlayerEnvironment);
    }

    public Task<IMetadata?>? GetMetadataFromStream(WrappedStream stream,
        IEnumerable<IMetadata?>? additionalMetadata = null)
    {
        try
        {
            var listofmetadatas = MetadataProviders?.Where(x => x.CanGetMetadata(stream))
                .Select(async x => await x.GetMetadata(stream)).Select(t => t.Result).Where(i => i != null).ToList();
            if (additionalMetadata != null)
            {
                listofmetadatas.AddRange(additionalMetadata.Where(x => x is not null));
            }

            return Task.FromResult<IMetadata?>(new MetadataCombo(listofmetadatas, MetadataGatherers));
        }
        catch (Exception e)
        {
            Log.Error(e, "Error loading metadata from stream");
            return null;
        }
    }


    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return files.Where(x => !(
            x == null ||
            x.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".docx", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".md", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".7z", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".rar", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".toml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".nfo", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".html", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".xmp", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".log", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".gif", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".cue", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".fpl", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".htm", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".pkf", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".db", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".webp", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".spotdl-cache", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".log", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".lrc", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".accurip", StringComparison.InvariantCultureIgnoreCase)
        ));
    }

    public List<Song> GetQueueCopy()
    {
        List<Song> r = [];
        if (PlayerContext.Queue is AvaloniaList<Song>)
        {
            Dispatcher.UIThread.Invoke(() => r = [.. PlayerContext.Queue]);
        }
        else
        {
            r = new List<Song>(PlayerContext.Queue);
        }

        return r;
    }

    private static Tuple<List<string>, List<string>> SupportedTrackMimesG(IEnumerable<MimeType> supportedMimes)
    {
        var enumerable = supportedMimes.ToList();
        var mimeTypes = enumerable.Select(x => x.Common).ToList();
        mimeTypes.AddRange(enumerable.SelectMany(y => y.AlternativeTypes));
        var fileExt = enumerable.SelectMany(x => x.FileExtensions).ToList();
        return new(mimeTypes, fileExt);
    }

    public Tuple<List<string>, List<string>> SupportedTrackMimes()
    {
        return SupportedTrackMimesG(PlayableMimes);
    }

    public Tuple<List<string>, List<string>> SupportedPlaylistMimes()
    {
        var playlistMimes = PlaylistReaders.SelectMany(x => x.SupportedMimes).Distinct();
        return SupportedTrackMimesG(playlistMimes);
    }

    public Tuple<List<string>, List<string>> SupportedTrackAndPlaylistMimes()
    {
        var (a, b) = SupportedTrackMimesG(PlayableMimes);
        var (c, d) = SupportedPlaylistMimes();
        a.AddRange(c);
        b.AddRange(d);
        return new(a, b);
    }
}
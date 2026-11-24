using System.Diagnostics;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class PlaybackEngine
{
    private readonly PlayerContext _playerContext;
    private readonly QueueManager _queueManager;
    private readonly PluginRegistry _pluginRegistry;
    private readonly MusicStatusCoordinator _musicStatusCoordinator;
    private readonly IPlayerUiBridge _uiBridge;
    private readonly Func<IFullPlayerEnvironment> _getEnvironment;
    private readonly ILogger? _log;
    private readonly Lock _gaplessEnqueueLock = new();

    private int _changingTrack;
    private int _handlingPlaybackStopped;
    private long _playbackGeneration;
    private Song? _enqueuedGaplessSong;
    public bool StopAutoLoading { get; set; }

    public IPlay? Player { get; set; }

    public PlaybackEngine(
        PlayerContext playerContext,
        QueueManager queueManager,
        PluginRegistry pluginRegistry,
        MusicStatusCoordinator musicStatusCoordinator,
        IPlayerUiBridge uiBridge,
        Func<IFullPlayerEnvironment> getEnvironment,
        ILogger? log = null)
    {
        _playerContext = playerContext ?? throw new ArgumentNullException(nameof(playerContext));
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));
        _musicStatusCoordinator =
            musicStatusCoordinator ?? throw new ArgumentNullException(nameof(musicStatusCoordinator));
        _uiBridge = uiBridge ?? throw new ArgumentNullException(nameof(uiBridge));
        _getEnvironment = getEnvironment ?? throw new ArgumentNullException(nameof(getEnvironment));
        _log = log;

        _queueManager.QueueModified += OnQueueModified;
    }

    private void OnQueueModified()
    {
        if (_playerContext.CurrentSong != null && Player is IPlayLancer)
        {
            TryEnqueueNextSongForGaplessPlayback();
        }
    }

    public void SendIfStateIsNotNull()
    {
        var state = Player?.GetPlaybackState();
        _musicStatusCoordinator.PlaybackStateChangedNotification(
            state ?? PlaybackState.Stopped,
            _pluginRegistry.WakeLockInterfaces);
    }

    public void PlayPause(bool allowStart)
    {
        if (Player?.GetPlaybackState() != PlaybackState.Playing)
        {
            if (Player?.GetPlaybackState() == PlaybackState.Paused || allowStart)
            {
                Play();
            }
        }
        else
        {
            Pause();
        }
    }

    public void Play()
    {
        if (Player != null && Player.IsTrackLoaded)
        {
            _log?.Debug("PlaybackEngine.Play() - Playing existing loaded track");
            Player.Play();
            SendIfStateIsNotNull();
        }
        else
        {
            _log?.Debug("PlaybackEngine.Play() - No track loaded, calling StartPlaying()");
            StartPlaying();
        }
    }

    public void Pause()
    {
        _log?.Debug("PlaybackEngine.Pause() called");
        Player?.Pause();
        SendIfStateIsNotNull();
    }

    public void Next()
    {
        if (_playerContext.CurrentSong == null) return;
        if (Interlocked.CompareExchange(ref _changingTrack, 1, 0) != 0) return;

        try
        {
            var queue = _queueManager.GetQueueCopy();
            var a = queue.IndexOf(_playerContext.CurrentSong);
            if (a == -1) return;

            Song? targetSong = null;
            if (a + 1 < queue.Count)
            {
                targetSong = queue[a + 1];
            }
            else if (_playerContext.LoopType == RepeatState.Queue && queue.Count > 0)
            {
                targetSong = queue[0];
            }

            if (targetSong == null) return;
            HandleSongChanging(targetSong);
        }
        finally
        {
            Volatile.Write(ref _changingTrack, 0);
        }
    }

    public void Previous()
    {
        if (_playerContext.CurrentSong == null) return;
        if (Interlocked.CompareExchange(ref _changingTrack, 1, 0) != 0) return;

        try
        {
            var queue = _queueManager.GetQueueCopy();
            var a = queue.IndexOf(_playerContext.CurrentSong);
            if (a <= 0) return;
            HandleSongChanging(queue[a - 1]);
        }
        finally
        {
            Volatile.Write(ref _changingTrack, 0);
        }
    }

    public void SetPosition(double seconds) => SetPosition(TimeSpan.FromSeconds(seconds));

    public void SetPosition(TimeSpan pos) => Player?.SetPosition(pos);

    public void SetVolume(byte volume)
    {
        Player?.SetVolume(volume);
    }

    public void SetLoopMode(RepeatState repeatState)
    {
        if (Player is not ILoop loop) return;
        if (repeatState == RepeatState.One)
        {
            loop.EnableLoop();
        }
        else
        {
            loop.DisableLoop();
        }
    }

    public void RemoveTrack()
    {
        _log?.Debug("PlaybackEngine.RemoveTrack() called, stopping Player");
        Interlocked.Increment(ref _playbackGeneration);
        lock (_gaplessEnqueueLock)
        {
            _enqueuedGaplessSong = null;
        }

        Player?.Stop();
        SendIfStateIsNotNull();
    }

    public void StartPlaying(bool play = true, bool resetsal = false)
    {
        StartPlayingAsync(play, resetsal).GetAwaiter().GetResult();
    }

    public async Task StartPlayingAsync(bool play = true, bool resetsal = false)
    {
        _log?.Information(
            "PlaybackEngine.StartPlayingAsync called (play={Play}, resetsal={Resetsal}, currentSong={CurrentSong})",
            play, resetsal, _playerContext.CurrentSong?.Name);

        if (_playerContext.CurrentSong == null)
        {
            var queue = _queueManager.GetQueueCopy();
            if (queue.Count > 0)
            {
                _playerContext.CurrentSong = queue[0];
                if (queue.Count >= 2)
                {
                    _queueManager.NextSong = queue[1];
                }
            }
            else
            {
                _log?.Information("Avoiding fatal crash, nothing to play");
                return;
            }
        }

        Interlocked.Increment(ref _playbackGeneration);
        lock (_gaplessEnqueueLock)
        {
            _enqueuedGaplessSong = null;
        }

        _log?.Debug("PlaybackEngine.StartPlayingAsync stopping existing player before load");
        Player?.Stop();
        var newPlayerRequired = true;
        if (Player is IPlayStreams p && p.SupportedMimes?.Contains(_playerContext.CurrentSong.Stream.MimeType) == true)
        {
            newPlayerRequired = false;
            _log?.Debug("Reusing existing player ({PlayerType}) for stream mime {Mime}", Player.GetType().Name,
                _playerContext.CurrentSong.Stream.MimeType);
            try
            {
                p.LoadStream(_playerContext.CurrentSong.Stream);
            }
            catch (Exception e)
            {
                _log?.Error(e, "EXCEPTION during LoadStream on reused player");
                Player.PositionChanged -= OnPositionChanged;
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
                if (Player is IPlayLancer oldLancer)
                {
                    oldLancer.NextStreamStarted -= OnNextStreamStarted;
                }

                Player.Dispose();
                Player = null;
            }
        }
        else
        {
            _log?.Debug("Requesting new player for stream mime {Mime}", _playerContext.CurrentSong.Stream.MimeType);
            if (Player != null)
            {
                Player.PositionChanged -= OnPositionChanged;
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
                if (Player is IPlayLancer oldLancer)
                {
                    oldLancer.NextStreamStarted -= OnNextStreamStarted;
                }

                Player.Dispose();
            }

            try
            {
                Player = await _pluginRegistry.GetPlayerFromStreamAsync(_playerContext.CurrentSong.Stream,
                    _getEnvironment());
                if (Player != null)
                {
                    Player.PositionChanged += OnPositionChanged;
                    Player.TrackEnd += OutputDevice_PlaybackStopped;
                    if (Player is IPlayLancer newLancer)
                    {
                        newLancer.NextStreamStarted += OnNextStreamStarted;
                    }
                }
            }
            catch (Exception e)
            {
                _log?.Error(e, "EXCEPTION during startplaying");
                Player?.Dispose();
                Player = null;
            }
        }

        if (Player == null)
        {
            _uiBridge.ShowMessageBox("Error", "I do not know how to play " + _playerContext.CurrentSong.URI);
            return;
        }

        _log?.Information("Got player of type {PlayerType} New: {NewPlayer}", Player.GetType(), newPlayerRequired);
        _musicStatusCoordinator.TrackChangedNotification(_playerContext.CurrentSong);
        _queueManager.UpdateNextSongReference();

        if (Player is IPlayLancer)
        {
            // Opening the next decoder can block on HTTP. Prepare it away from
            // the UI and playback callback threads.
            TryEnqueueNextSongForGaplessPlayback();
        }

        if (play)
        {
            Player.SetVolume(_playerContext.Volume);
            _log?.Debug("Calling Player.Play()");
            Player.Play();
            var currentPlayer = Player;
            var startedGeneration = Volatile.Read(ref _playbackGeneration);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(600);
                    if (Volatile.Read(ref _playbackGeneration) == startedGeneration &&
                        ReferenceEquals(Player, currentPlayer) &&
                        currentPlayer is ILoop loop)
                    {
                        if (_playerContext.LoopType == RepeatState.One)
                        {
                            loop.EnableLoop();
                        }
                        else
                        {
                            loop.DisableLoop();
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, "Failed configuring delayed loop state");
                }
            });

            SendIfStateIsNotNull();

            _uiBridge.ResetUiScrollBar();
            if (Player?.Length() is { } total)
            {
                _uiBridge.SetScrollBarTextTo(total);
            }

            _uiBridge.HandleLateStageMetadataAndScrollBar();
        }

        if (!resetsal) return;
        _log?.Information("StopAutoLoading set to false in StartPlaying (resetsal=true)");
        StopAutoLoading = false;
    }

    public void HandleSongChanging(Song nextSong)
    {
        ArgumentNullException.ThrowIfNull(nextSong);
        _log?.Information(
            "HandleSongChanging: nextSong={NextSong}, currentSong={CurrentSong}, current StopAutoLoading={StopAutoLoading}",
            nextSong.Name, _playerContext.CurrentSong?.Name, StopAutoLoading);
        StopAutoLoading = true;
        _playerContext.CurrentSong = nextSong;
        Debug.Assert(_playerContext.CurrentSong.Equals(nextSong));
        _log?.Debug("HandleSongChanging: Calling RemoveTrack()");
        RemoveTrack();
        _log?.Debug("HandleSongChanging: Calling StartPlaying(resetsal: true)");
        StartPlaying(resetsal: true);
        _log?.Information("HandleSongChanging finished for {Song}", nextSong.Name);
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        if (Interlocked.Exchange(ref _handlingPlaybackStopped, 1) != 0) return;

        var expectedGeneration = Volatile.Read(ref _playbackGeneration);
        var expectedPlayer = Player;

        _ = Task.Run(() =>
        {
            try
            {
                if (Volatile.Read(ref _playbackGeneration) != expectedGeneration ||
                    !ReferenceEquals(Player, expectedPlayer))
                {
                    _log?.Debug("Ignoring stale PlaybackStopped callback for superseded playback generation");
                    return;
                }

                HandlePlaybackStopped(sender, o);
            }
            catch (Exception e)
            {
                _log?.Error(e, "Failed while advancing playback after track end");
            }
            finally
            {
                Volatile.Write(ref _handlingPlaybackStopped, 0);
            }
        });
    }

    private void HandlePlaybackStopped(object? sender, object o)
    {
        _log?.Information("""
                          Output device playback stopped
                          Sender: {SenderType}
                          EventArgs: {EventArgs}
                          StopAutoLoading: {StopAutoLoading}
                          Current song: {CurrentSong}
                          Loop mode: {LoopType}
                          """, sender?.GetType().FullName, o, StopAutoLoading, _playerContext.CurrentSong,
            _playerContext.LoopType);

        if (StopAutoLoading)
        {
            _log?.Information("OutputDevice_PlaybackStopped ignored because StopAutoLoading is true");
            return;
        }

        if (_playerContext.LoopType == RepeatState.One)
        {
            _log?.Information("Playback stopped with LoopType == RepeatState.One, re-triggering StartPlaying()");
            StartPlaying();
        }
        else if (_playerContext.CurrentSong != null)
        {
            var queue = _queueManager.GetQueueCopy();
            var currentTrackIndex = queue.IndexOf(_playerContext.CurrentSong);
            _log?.Information("Playback stopped naturally. CurrentTrackIndex: {Index} / {Count}", currentTrackIndex,
                queue.Count);
            if (currentTrackIndex != -1)
            {
                if (currentTrackIndex + 1 < queue.Count)
                {
                    _log?.Information("Advancing to next queue item [{NextIndex}]: {NextSong}", currentTrackIndex + 1,
                        queue[currentTrackIndex + 1].Name);
                    HandleSongChanging(queue[currentTrackIndex + 1]);
                }
                else if (_playerContext.LoopType == RepeatState.Queue && queue.Count > 0)
                {
                    _log?.Information("Loop mode is Queue, looping back to index 0: {FirstSong}", queue[0].Name);
                    HandleSongChanging(queue[0]);
                }
                else
                {
                    _log?.Information("End of queue reached, calling RemoveTrack in PlaybackStopped");
                    RemoveTrack();
                }
            }
            else if (_queueManager.NextSong != null)
            {
                _log?.Information("Advancing to NextSong from queue manager: {NextSong}", _queueManager.NextSong.Name);
                var previousSong = _playerContext.CurrentSong;
                HandleSongChanging(_queueManager.NextSong);
                previousSong?.Dispose();
                _queueManager.NextSong = null;
            }
            else
            {
                _log?.Information("No further song to play, clearing CurrentSong");
                var previousSong = _playerContext.CurrentSong;
                _playerContext.CurrentSong = null;
                RemoveTrack();
                previousSong?.Dispose();
            }
        }
    }

    public void TryEnqueueNextSongForGaplessPlayback()
    {
        _ = Task.Run(EnqueueNextSongForGaplessPlayback);
    }

    private void EnqueueNextSongForGaplessPlayback()
    {
        lock (_gaplessEnqueueLock)
        {
            if (Player is not IPlayLancer lancer) return;
            if (_playerContext.CurrentSong == null) return;

            var nextSong = _queueManager.GetNextSongInQueue();
            if (nextSong == null) return;

            if (ReferenceEquals(nextSong, _enqueuedGaplessSong)) return;

            try
            {
                if (_enqueuedGaplessSong != null)
                {
                    if (lancer.ReplaceNextStream(nextSong.Stream))
                    {
                        _enqueuedGaplessSong = nextSong;
                        _queueManager.NextSong = nextSong;
                        _log?.Debug("Replaced queued stream for gapless playback: {NextSong}", nextSong.URI);
                        return;
                    }
                }

                if (lancer.EnqueueNextStream(nextSong.Stream))
                {
                    _enqueuedGaplessSong = nextSong;
                    _queueManager.NextSong = nextSong;
                    _log?.Debug("Enqueued next stream for gapless playback: {NextSong}", nextSong.URI);
                }
                else
                {
                    _log?.Debug("Player refused to enqueue next stream: {NextSong}", nextSong.URI);
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex, "Failed to enqueue next stream: {NextSong}", nextSong.URI);
            }
        }
    }

    public bool ReplaceNextStreamForGaplessPlayback(Song nextSong)
    {
        if (nextSong == null) return false;
        lock (_gaplessEnqueueLock)
        {
            if (Player is not IPlayLancer lancer) return false;
            try
            {
                var replaced = lancer.ReplaceNextStream(nextSong.Stream);
                if (replaced)
                {
                    _enqueuedGaplessSong = nextSong;
                }

                return replaced;
            }
            catch (Exception e)
            {
                _log?.Error(e, "Failed to replace next stream for gapless playback");
                return false;
            }
        }
    }

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        _musicStatusCoordinator.FirePositionChanged(e);
    }

    private void OnNextStreamStarted(object? sender, EventArgs e)
    {
        Song? promotedSong;
        lock (_gaplessEnqueueLock)
        {
            promotedSong = _enqueuedGaplessSong ?? _queueManager.GetNextSongInQueue();
            _enqueuedGaplessSong = null;
        }

        _log?.Information(
            "PlaybackEngine.OnNextStreamStarted fired from Gapless provider. Promoted song: {PromotedSong}",
            promotedSong?.Name);
        if (promotedSong == null) return;

        var expectedPlayer = sender ?? Player;
        _uiBridge.PostToUi(() =>
        {
            if (!ReferenceEquals(Player, expectedPlayer)) return;
            _playerContext.CurrentSong = promotedSong;
            _queueManager.UpdateNextSongReference();
            _musicStatusCoordinator.TrackChangedNotification(promotedSong);
            _uiBridge.ResetUiScrollBar();
            if (Player?.Length() is { } total)
            {
                _uiBridge.SetScrollBarTextTo(total);
            }

            _uiBridge.HandleLateStageMetadataAndScrollBar();

            TryEnqueueNextSongForGaplessPlayback();
        });
    }
}
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

    private bool _changeAllowed = true;
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
        _musicStatusCoordinator = musicStatusCoordinator ?? throw new ArgumentNullException(nameof(musicStatusCoordinator));
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
        if (!_changeAllowed || _playerContext.CurrentSong == null) return;
        lock (_playerContext.Queue)
        {
            var a = _playerContext.Queue.IndexOf(_playerContext.CurrentSong);
            if (a == -1 || a + 1 >= _playerContext.Queue.Count) return;
            _changeAllowed = false;
            _log?.Information("PlaybackEngine.Next() clicked, moving to index {Index}: {Song}", a + 1, _playerContext.Queue[a + 1]);
            HandleSongChanging(_playerContext.Queue[a + 1]);
            _changeAllowed = true;
        }
    }

    public void Previous()
    {
        if (!_changeAllowed || _playerContext.CurrentSong == null) return;
        lock (_playerContext.Queue)
        {
            var a = _playerContext.Queue.IndexOf(_playerContext.CurrentSong);
            if (a == -1 || a - 1 < 0) return;
            _changeAllowed = false;
            _log?.Information("PlaybackEngine.Previous() clicked, moving to index {Index}: {Song}", a - 1, _playerContext.Queue[a - 1]);
            HandleSongChanging(_playerContext.Queue[a - 1]);
            _changeAllowed = true;
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
        Player?.Stop();
        SendIfStateIsNotNull();
    }

    public void StartPlaying(bool play = true, bool resetsal = false)
    {
        StartPlayingAsync(play, resetsal).GetAwaiter().GetResult();
    }

    public async Task StartPlayingAsync(bool play = true, bool resetsal = false)
    {
        _log?.Information("PlaybackEngine.StartPlayingAsync called (play={Play}, resetsal={Resetsal}, currentSong={CurrentSong})",
            play, resetsal, _playerContext.CurrentSong?.Name);

        if (_playerContext.CurrentSong == null)
        {
            if (_playerContext.Queue.Count > 0)
            {
                _playerContext.CurrentSong = _playerContext.Queue[0];
                if (_playerContext.Queue.Count >= 2)
                {
                    _queueManager.NextSong = _playerContext.Queue[1];
                }
            }
            else
            {
                _log?.Information("Avoiding fatal crash, nothing to play");
                return;
            }
        }

        _log?.Debug("PlaybackEngine.StartPlayingAsync stopping existing player before load");
        Player?.Stop();
        var newPlayerRequired = true;
        if (Player is IPlayStreams p && p.SupportedMimes?.Contains(_playerContext.CurrentSong.Stream.MimeType) == true)
        {
            newPlayerRequired = false;
            _log?.Debug("Reusing existing player ({PlayerType}) for stream mime {Mime}", Player.GetType().Name, _playerContext.CurrentSong.Stream.MimeType);
            p.LoadStream(_playerContext.CurrentSong.Stream);
        }
        else
        {
            _log?.Debug("Requesting new player for stream mime {Mime}", _playerContext.CurrentSong.Stream.MimeType);
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
                Player = await _pluginRegistry.GetPlayerFromStreamAsync(_playerContext.CurrentSong.Stream, _getEnvironment());
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

        if (Player is IPlayLancer lancer)
        {
            lancer.NextStreamStarted -= OnNextStreamStarted;
            lancer.NextStreamStarted += OnNextStreamStarted;

            var nextSong = _queueManager.GetNextSongInQueue();
            if (nextSong != null)
            {
                try
                {
                    if (lancer.EnqueueNextStream(nextSong.Stream))
                    {
                        _log?.Debug("Enqueued next stream for gapless playback: {NextSong}", nextSong.URI);
                    }
                    else
                    {
                        _log?.Debug("Player refused to enqueue next stream: {NextSong}", nextSong.URI);
                    }
                }
                catch (Exception e)
                {
                    _log?.Error(e, "Failed to enqueue next stream for {NextSong}", nextSong.URI);
                }
            }
        }

        if (play)
        {
            Player.SetVolume(_playerContext.Volume);
            _log?.Debug("Calling Player.Play()");
            Player.Play();
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                if (Player is ILoop loop)
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
            });

            SendIfStateIsNotNull();
            if (newPlayerRequired)
            {
                _log?.Debug("Attaching OutputDevice_PlaybackStopped to Player.TrackEnd");
                Player.TrackEnd += OutputDevice_PlaybackStopped;
            }

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
        _log?.Information("HandleSongChanging: nextSong={NextSong}, currentSong={CurrentSong}, current StopAutoLoading={StopAutoLoading}",
            nextSong?.Name, _playerContext.CurrentSong?.Name, StopAutoLoading);
        StopAutoLoading = true;
        _playerContext.CurrentSong = nextSong;
        Debug.Assert(_playerContext.CurrentSong.Equals(nextSong));
        _log?.Debug("HandleSongChanging: Calling RemoveTrack()");
        RemoveTrack();
        _log?.Debug("HandleSongChanging: Calling StartPlaying(resetsal: true)");
        StartPlaying(resetsal: true);
        _log?.Information("HandleSongChanging finished for {Song}", nextSong?.Name);
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        _log?.Information("""
                          Output device playback stopped
                          Sender: {SenderType}
                          EventArgs: {EventArgs}
                          StopAutoLoading: {StopAutoLoading}
                          Current song: {CurrentSong}
                          Loop mode: {LoopType}
                          """, sender?.GetType().FullName, o, StopAutoLoading, _playerContext.CurrentSong, _playerContext.LoopType);

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
            var currentTrackIndex = _playerContext.Queue.IndexOf(_playerContext.CurrentSong);
            _log?.Information("Playback stopped naturally. CurrentTrackIndex: {Index} / {Count}", currentTrackIndex, _playerContext.Queue.Count);
            if (currentTrackIndex != -1)
            {
                if (currentTrackIndex + 1 < _playerContext.Queue.Count)
                {
                    _log?.Information("Advancing to next queue item [{NextIndex}]: {NextSong}", currentTrackIndex + 1, _playerContext.Queue[currentTrackIndex + 1].Name);
                    HandleSongChanging(_playerContext.Queue[currentTrackIndex + 1]);
                }
                else if (_playerContext.LoopType == RepeatState.Queue)
                {
                    _log?.Information("Loop mode is Queue, looping back to index 0: {FirstSong}", _playerContext.Queue[0].Name);
                    HandleSongChanging(_playerContext.Queue[0]);
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
                _playerContext.CurrentSong?.Dispose();
                HandleSongChanging(_queueManager.NextSong);
                _queueManager.NextSong = null;
            }
            else
            {
                _log?.Information("No further song to play, clearing CurrentSong");
                _playerContext.CurrentSong?.Dispose();
                _playerContext.CurrentSong = null;
            }
        }
    }

    public void TryEnqueueNextSongForGaplessPlayback()
    {
        if (Player is not IPlayLancer lancer) return;
        if (_playerContext.CurrentSong == null) return;

        var nextSong = _queueManager.GetNextSongInQueue();
        if (nextSong == null) return;

        if (_queueManager.NextSong != null && ReferenceEquals(nextSong, _queueManager.NextSong))
        {
            return;
        }

        try
        {
            if (lancer.EnqueueNextStream(nextSong.Stream))
            {
                _queueManager.NextSong = nextSong;
                _log?.Debug("Enqueued next stream for gapless playback after add: {NextSong}", nextSong.URI);
            }
            else
            {
                _log?.Debug("Player refused to enqueue next stream after add: {NextSong}", nextSong.URI);
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "Failed to enqueue next stream after add: {NextSong}", nextSong.URI);
        }
    }

    public bool ReplaceNextStreamForGaplessPlayback(Song nextSong)
    {
        if (nextSong == null) return false;
        if (Player is not IPlayLancer lancer) return false;
        try
        {
            return lancer.ReplaceNextStream(nextSong.Stream);
        }
        catch (Exception e)
        {
            _log?.Error(e, "Failed to replace next stream for gapless playback");
            return false;
        }
    }

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        _musicStatusCoordinator.FirePositionChanged(e);
    }

    private void OnNextStreamStarted(object? sender, EventArgs e)
    {
        var promotedSong = _queueManager.GetNextSongInQueue();
        _log?.Information("PlaybackEngine.OnNextStreamStarted fired from Gapless provider. Promoted song: {PromotedSong}", promotedSong?.Name);
        if (promotedSong == null) return;

        _uiBridge.PostToUi(() =>
        {
            _playerContext.CurrentSong = promotedSong;
            _queueManager.UpdateNextSongReference();
            _musicStatusCoordinator.TrackChangedNotification(promotedSong);
            _uiBridge.ResetUiScrollBar();
            if (Player?.Length() is { } total)
            {
                _uiBridge.SetScrollBarTextTo(total);
            }
            _uiBridge.HandleLateStageMetadataAndScrollBar();

            var followingSong = _queueManager.GetNextSongInQueue();
            if (followingSong != null && Player is IPlayLancer lancer)
            {
                try
                {
                    if (lancer.EnqueueNextStream(followingSong.Stream))
                    {
                        _log?.Debug("Enqueued following stream for gapless playback: {NextSong}", followingSong.URI);
                    }
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, "Failed to enqueue following stream for {NextSong}", followingSong.URI);
                }
            }
        });
    }
}

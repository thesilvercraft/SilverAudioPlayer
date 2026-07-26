using System.Diagnostics;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;
using SilverMagicBytes;

namespace SilverAudioPlayer.Core;


public abstract class BaseEnvironment<T> : IHaveConfigFilesWithKnownLocations, IFullPlayerEnvironment where T : PlayerContext
{

    public event EventHandler<Song>? TrackChangedNotification;
    public event EventHandler<PlaybackState> PlayerStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    private SortedSet<TimedEvent> _timedEvents = new();
    Logic<T> Logic;
    private bool _timerEnabled = false;
    private bool _isDisposed = false;
    private Timer? _timer=null; // TODO: DISPOSE
    protected internal IConfigPathProvider _configPathProvider;
    public BaseEnvironment(Logic<T> Logic, IConfigPathProvider configPathProvider)
    {
        this.Logic = Logic;
        this._configPathProvider=configPathProvider;
        _timer = new Timer(TimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }
   
    public void LoadSong(WrappedStream s) => Task.Run(() => Logic.LoadSong(s));

    public void LoadSong(WrappedStream s, IMetadata additionalMetadata) => Task.Run(() => Logic.LoadSong(s,additionalMetadata));

    public void ProcessFiles(IEnumerable<string> files) => Logic.ProcessFiles(files);

    public void LoadSongs(IEnumerable<WrappedStream> streams) => Logic.LoadSongs(streams);
    public void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams) => Logic.LoadSongs(streams);

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return Logic.FilterFiles(files);
    }

    public virtual string Name { get; }

    public virtual string Description  { get; }

    public virtual WrappedStream? Icon { get; }

    public virtual Version? Version { get; }
    public virtual List<Tuple<Uri, URLType>>? Links { get; }

    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream)
    {
        return Logic.GetMetadataFromStream(stream);
    }

    public virtual async Task<List<Song>?> GetQueue()
    {
        //TODO ASK USER BEFORE GIVING OVER QUEUE
        return Logic.GetQueueCopy();
    }

    public IPicture? GetBestRepresentation(IReadOnlyList<IPicture>? pictures, PictureType type = PictureType.Front)
    {
        if(pictures is null || pictures.Count==0)
        {
            return null;
        }
        var res = pictures?.Where(x => x.PicType == type);
        return res?.Any() == true ? res?.First() : pictures[0];
    }

    public void Play() => Logic.Play();

    public void Pause() => Logic.Pause();

    public void PlayPause() => Logic.PlayPause(true);

    public void Stop() => Logic.RemoveTrack();

    public void Next() => Logic.Next();

    public void Previous() => Logic.Previous();

    public void SetVolume(byte volume) => Logic.PlayerContext.Volume = volume;

    public byte GetVolume() => Logic.PlayerContext.Volume;

    public Song? GetCurrentTrack()
    {
        return Logic.PlayerContext.CurrentSong;
    }
    public IDictionary<string, string>? GetEntriesForCurrentTrack()
    {
        return Logic.PlayerContext?.CurrentSong?.Metadata?.Entries;
    }

    public double GetDuration() => Logic.Player?.Length()?.TotalSeconds ??
                                   (Logic.PlayerContext.CurrentSong?.Metadata?.Duration / 1000) ?? 2;


    public void SetPosition(double position) => Logic.Player?.SetPosition(TimeSpan.FromSeconds(position));

    public double GetPosition() => Logic.Player?.GetPosition().TotalSeconds ?? 1;

    public PlaybackState GetState()
    {
        var x = Logic.Player?.GetPlaybackState();
        if (x != null)
        {
            return (PlaybackState)x;
        }

        return PlaybackState.Stopped;
    }

    public RepeatState GetRepeat() => Logic.PlayerContext.LoopType;

    public void SetRepeat(RepeatState state) => Logic.PlayerContext.LoopType = state;

    public bool GetShuffle()
    {
        return false;
    }

    public void SetShuffle(bool shuffle)
    {
        //TODO SHUFFLE
    }

    public virtual void SetRating(byte rating)
    {
    }


    public string GetLyrics()
    {
        return Logic.PlayerContext.CurrentSong?.Metadata?.Lyrics;
    }

    public void FireTrackChangedNotification(Song? currentSong)
    {
        ClearTimedEvents();
        Task.Run(() => { TrackChangedNotification?.Invoke(this, currentSong); });
    }
    
    void ClearTimedEvents()
    {
        lock (_timedEvents)
        {
            foreach (var e in _timedEvents)
            {
                e.RemoveFromParent();
            }
            _timedEvents.Clear();
        }
        StopTimer();
    }
    public void FirePositionChanged(TimeSpan position)
    {
        Task.Run(() => { PositionChanged?.Invoke(this, position); });
        TimerLogicOnPositionChanged(position.TotalSeconds);
    }

    public void FirePlayerStateChanged(PlaybackState state)
    {
        Task.Run(() => { PlayerStateChanged?.Invoke(this, state); });
        TimerLogicOnStateChanged(state);
    }


    public virtual string Licenses { get; }


    public virtual string[] KnownConfigFileLocations =>
    [
        _configPathProvider.GetPath("SilverAudioPlayer.Config.xml"),
    ];

    public virtual IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements)
    {
        return null;
    }

    public Task<IReadOnlyList<MimeType>> SupportedMimeTypes() => Task.FromResult<IReadOnlyList<MimeType>>(Logic.PlayableMimes);

    void TimerLogicOnPositionChanged(double positionTotalSeconds)
    {
        TimerLogicOnStateChanged(GetState());
    }
    void TimerLogicOnStateChanged(PlaybackState state)
    {
        switch (state)
        {
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                StopTimer();
                break;

            case PlaybackState.Playing:
            case PlaybackState.Buffering:
                SetTimerToNextEvent();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }
    private void StopTimer()
    {
        lock (_timedEvents)
        {
            _timerEnabled = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    void SetTimerToNextEvent()
    {
        lock (_timedEvents)
        {
            if (_isDisposed || _timedEvents.Count == 0)
            {
                _timerEnabled = false;
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            var currentPos = GetPosition();
            var next = _timedEvents.FirstOrDefault(x => x.Time > currentPos);
            
            if (next != null)
            {
                _timerEnabled = true;
                var delay = Math.Max(next.Time - currentPos - 0.01, 0.0); 
                _timer?.Change(TimeSpan.FromSeconds(delay), Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timerEnabled = false;
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }
    public void AddTimedEvent(TimedEvent timedEvent)
    {
        lock (_timedEvents)
        {
            _timedEvents.Add(timedEvent);
            timedEvent.AddToParent(this);

            if (GetState() == PlaybackState.Playing || GetState() == PlaybackState.Buffering)
            {
                SetTimerToNextEvent();
            }
        }
    }
    public void TimerElapsed(object? state)
    {
        if (_isDisposed) return;

        List<TimedEvent> eventsToInvoke;
        var position = GetPosition();
        lock (_timedEvents)
        {
            eventsToInvoke = _timedEvents.Where(x => x.Time <= position + 0.02).ToList();
        }

        if (eventsToInvoke.Count > 0)
        {
            var track = GetCurrentTrack();
            foreach (var e in eventsToInvoke)
            {
                e.Invoke(track, position);
            }
        }
        SetTimerToNextEvent();
    }
    

    public void RemoveTimedEvent(TimedEvent timedEvent)
    { 
        lock (_timedEvents)
        {
            if (_timedEvents.Remove(timedEvent))
            {
                timedEvent.RemoveFromParent();
            }
        }
    }
}
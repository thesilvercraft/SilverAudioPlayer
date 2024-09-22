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

    public BaseEnvironment(Logic<T> Logic)
    {
        this.Logic = Logic;
    }
   
    Logic<T> Logic;
    public void LoadSong(WrappedStream s)
    {
        Task.Run(() => Logic.ProcessStream(s));
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        Logic.ProcessFiles(files);
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        Logic.ProcessStreams(streams);
    }

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
        //A music player should probably not edit the metadata of the music it plays
        //If someone thinks otherwise feel free to add code to this method
    }


    public string GetLyrics()
    {
        return Logic.PlayerContext.CurrentSong?.Metadata?.Lyrics;
    }

    public void FireTrackChangedNotification(Song? currentSong)
    {
        Task.Run(() => { TrackChangedNotification?.Invoke(this, currentSong); });
        ClearTimedEvents();
    }

    void ClearTimedEvents()
    {
        TimedEvents.Clear();
        
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
        ConfigPath.GetPath("SilverAudioPlayer.Config.xml"),
    ];

    public virtual IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements)
    {
        // EkonrensisWindow window = new();
        //window.HandleConfiguration(elements);
        return null;
    }

    public Task<IReadOnlyList<MimeType>> SupportedMimeTypes() => Task.FromResult<IReadOnlyList<MimeType>>(Logic.PlayableMimes);
    private SortedSet<TimedEvent> TimedEvents;

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
                timer.Change(Timeout.Infinite, Timeout.Infinite);
break;

            case PlaybackState.Playing:
            case PlaybackState.Buffering:
                SetTimerToNextEvent();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    void SetTimerToNextEvent()
    {
        if (TimedEvents.Count > 0)
        {
            var currentPos = GetPosition();
            var next =  TimedEvents.FirstOrDefault(x => x.Time > currentPos);
            if (next != null)
            {
                timer.Change(TimeSpan.FromSeconds(currentPos - next.Time), Timeout.InfiniteTimeSpan);
                return;
            }
        }
        timer.Change(Timeout.Infinite, Timeout.Infinite);
    }
    public void AddTimedEvent(TimedEvent timedEvent)
    {
        TimedEvents.Add(timedEvent);
        timedEvent.AddToParent(this);
        if (timer == null)
        {
            timer=new(TimerElapsed, null, TimeSpan.FromSeconds(TimedEvents.First().Time), Timeout.InfiniteTimeSpan);
        }
    }
    private Timer? timer=null;
    public void TimerElapsed(object? state)
    {
        var position = GetPosition();
        var events= TimedEvents.Where(x => Math.Abs(position - x.Time) < 0.04).ToList();
        var track = GetCurrentTrack();
        foreach (var e in events)
        {
            e.Invoke(track, position);
        }

        SetTimerToNextEvent();

    }
    

    public void RemoveTimedEvent(TimedEvent timedEvent)
    { 
        timedEvent.RemoveFromParent();
        TimedEvents.Remove(timedEvent);
    }
}
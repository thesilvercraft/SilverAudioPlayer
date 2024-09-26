
namespace SilverAudioPlayer.Shared;

public interface ITimingListener
{
    /// <summary>
    /// Add a new timed event to this listener
    /// <remarks>
    /// Call this method after you are sure the track has changed because timed events are cleared on track change
    /// </remarks>
    /// </summary>
    /// <param name="time">The time in seconds</param>
    /// <param name="action">The action to invoke</param>
    public void AddTimedEvent(double time, Func<Song,double, bool> action)
    {
        AddTimedEvent(new(time, action));
    }
    /// <summary>
    /// Add a new timed event to this listener
    /// <remarks>
    /// Call this method after you are sure the track has changed because timed events are cleared on track change
    /// </remarks>
    /// </summary>
    /// <param name="timedEvent">the timed event</param>
    public void AddTimedEvent(TimedEvent timedEvent);
    void RemoveTimedEvent(TimedEvent timedEvent);

}

public class TimedEvent : IComparer<TimedEvent>, IComparable<TimedEvent>
{
    private ITimingListener? parent=null;
    /// <summary>
    /// Time in seconds
    /// </summary>
    public double Time { get; private set; }
    private Func<Song,double, bool> Action { get; set; }

    /// <summary>
    /// Creates a new timed event
    /// </summary>
    /// <param name="time">Time in seconds</param>
    /// <param name="action">Action to execute </param>
    public TimedEvent(double time,Func<Song,double, bool> action)
    {
        Time = time;
        Action = action;
    }

    public void AddToParent(ITimingListener timingListener)
    {
        if (parent is not null) throw new InvalidOperationException("This object already has a parent");
        parent = timingListener;
    }

    public void RemoveFromParent()
    {
        if(parent is null) throw new InvalidOperationException("This object doesn't have a parent");
        parent = null;
    }
    public void Invoke(Song song, double time)
    {
        try
        {
            if (Action?.Invoke(song,time) is true)
            {
                return;
            }
        }
        catch (Exception e)
        {
            Serilog.Log.Error(e,"Error in TimedEvent Invoke");
        }

        parent?.RemoveTimedEvent(this);

    }

    public int Compare(TimedEvent? x, TimedEvent? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (y is null) return 1;
        if (x is null) return -1;
        return x.Time.CompareTo(y.Time);
    }

    public int CompareTo(TimedEvent? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return Time.CompareTo(other.Time);
    }
}
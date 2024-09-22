using System.Collections;

namespace SilverAudioPlayer.Shared;

public interface ITimingListener
{
    public void AddTimedEvent(double time, Func<Song,double, bool> action)
    {
        AddTimedEvent(new(time, action));
    }
    public void AddTimedEvent(TimedEvent timedEvent);
    void RemoveTimedEvent(TimedEvent timedEvent);

}

public class TimedEvent : IComparer<TimedEvent>
{
    private ITimingListener? parent=null;
    /// <summary>
    /// Time in seconds
    /// </summary>
    public double Time { get; private set; }
    private Func<Song,double, bool> Action { get; set; }

    /// <summary>
    /// 
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
}
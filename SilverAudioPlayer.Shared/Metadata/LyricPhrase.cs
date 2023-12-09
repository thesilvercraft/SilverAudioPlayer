namespace SilverAudioPlayer.Shared;

public class LyricPhrase
{
    public LyricPhrase(double timeStampInMilliSeconds, string content)
    {
        TimeStampInMilliSeconds = timeStampInMilliSeconds;
        Content = content;
    }

    public double TimeStampInMilliSeconds { get; set; }
    public string Content { get; set; }
    public override string ToString()
    {
        return $"{new _DONOTUSEINTERNALTIMESPAN(TimeStampInMilliSeconds)} {Content}";
    }
}
public class _DONOTUSEINTERNALTIMESPAN
{
    public _DONOTUSEINTERNALTIMESPAN(double milliseconds)
    {
        Milliseconds = milliseconds;
    }
    public _DONOTUSEINTERNALTIMESPAN(double minute, double second, double hundredth)
    {
        Milliseconds = hundredth * 10 + second * 1000 + minute * 60000;
    }
    public override string ToString()
    {
        return $"[{Minute}:{Second:00}.{Hundredth:00}]";
    }
    public double Milliseconds { get; }
    public int Minute => (int)(Milliseconds / (60 * 1000));
    public int Second => (int)(Milliseconds % (60 * 1000) / 1000);
    public int Hundredth => (int)(Milliseconds % 1000 / 10);
}
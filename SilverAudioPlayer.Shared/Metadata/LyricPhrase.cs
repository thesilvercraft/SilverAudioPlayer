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
}
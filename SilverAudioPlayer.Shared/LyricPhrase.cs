namespace SilverAudioPlayer.Shared
{
    public class LyricPhrase
    {
        public LyricPhrase(long timeStampInMilliSeconds, string content)
        {
            TimeStampInMilliSeconds = timeStampInMilliSeconds;
            Content = content;
        }

        public long TimeStampInMilliSeconds { get; set; }
        public string Content { get; set; }
    }
}
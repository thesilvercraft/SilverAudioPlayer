namespace SilverAudioPlayer.Shared
{
    public abstract class WrappedStream
    {
        public Stream Stream { get; set; }
        public string MimeType { get; set; }

        public virtual Stream RegenStream()
        {
            Stream.Position = 0;
            return Stream;
        }
    }
}
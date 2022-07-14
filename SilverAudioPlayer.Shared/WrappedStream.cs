namespace SilverAudioPlayer.Shared
{
    public abstract class WrappedStream
    {
        public abstract string MimeType { get; }

        public abstract Stream GetStream();
    }
}
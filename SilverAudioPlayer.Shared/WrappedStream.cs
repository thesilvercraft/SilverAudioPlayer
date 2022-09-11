using SilverMagicBytes;

namespace SilverAudioPlayer.Shared
{
    public abstract class WrappedStream
    {
        public abstract MimeType MimeType { get; }

        public abstract Stream GetStream();

    }
}
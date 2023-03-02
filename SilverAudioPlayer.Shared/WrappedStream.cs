using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public abstract class WrappedStream : IDisposable
{
    public abstract MimeType MimeType { get; }

    public abstract Stream GetStream();
    public abstract void Dispose();
}

public abstract class WrappedRegenerativeStream:WrappedStream
{
    /// <summary>
    /// A new stream sharing the same data as the previous one (if any)
    /// </summary>
    /// <returns>A read only stream</returns>
    public abstract override  Stream GetStream();
}
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public abstract class WrappedStream : IDisposable
{
    public abstract MimeType MimeType { get; }

    public abstract Stream GetStream();
    /// <summary>
    /// Should the code that got a stream from a GetStream call dispose on it after it is no longer needed
    /// </summary>
    public abstract bool ShouldDisposeStream { get; }
    public abstract void Dispose();
    /// <summary>
    /// A simple way to use this wrappedstream
    /// </summary>
    /// <param name="x">the action to perform with that stream</param>
    public void Use(Action<Stream> x)
    {
        var s = GetStream();
        try
        {
            x.Invoke(s);
        }
        finally
        {
            if (ShouldDisposeStream)
            {
                s.Dispose();
            }
        }
    }
    
}

public abstract class WrappedRegenerativeStream:WrappedStream
{
    /// <summary>
    /// A new stream sharing the same data as the previous one (if any)
    /// </summary>
    /// <returns>A read only stream</returns>
    public abstract override  Stream GetStream();
}
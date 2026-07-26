using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public abstract class WrappedStream : IDisposable
{
    public abstract MimeType MimeType { get; }

    /// <summary>
    ///     Returns whether this wrapper can create multiple independent stream instances.
    ///     Gapless playback and preloading rely on being able to open the stream again.
    /// </summary>
    public virtual bool CanCreateMultipleStreams => true;

    /// <summary>
    ///     Returns whether this wrapped stream can be seeked.
    /// </summary>
    public virtual bool IsSeekable => false;

    public abstract Stream GetStream();

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
            s.Dispose();
        }
    }
}


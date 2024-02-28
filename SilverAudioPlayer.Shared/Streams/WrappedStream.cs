using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public abstract class WrappedStream : IDisposable
{
    public abstract MimeType MimeType { get; }

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


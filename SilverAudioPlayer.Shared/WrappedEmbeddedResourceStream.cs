using System.Reflection;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedEmbeddedResourceStream : WrappedStream, IDisposable
{
    private readonly Assembly Assembly;
    private bool disposedValue;

    public WrappedEmbeddedResourceStream(Assembly assembly, string resourceLoc)
    {
        Assembly = assembly;
        URL = resourceLoc;
    }

    public string URL { get; set; }
    public List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private Stream InternalGetStream()
    {
        var Stream = Assembly.GetManifestResourceStream(URL);
        Streams.Add(Stream);
        return Stream;
    }

    public override Stream GetStream()
    {
        var Stream = InternalGetStream();
        MimeType? mt;
        using (var stream2 = InternalGetStream())
        {
            mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
            Streams.Remove(stream2);
        }

        _MimeType = mt;
        return Stream;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
                foreach (var stream in Streams.Where(x => x.CanRead))
                    stream.Dispose();
            disposedValue = true;
        }
    }
}
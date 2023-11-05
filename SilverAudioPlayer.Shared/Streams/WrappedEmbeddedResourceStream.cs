using System.Reflection;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedEmbeddedResourceStream : WrappedStream
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
    private MimeType? _MimeType { get; set; } = null;

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private Stream InternalGetStream()
    {
        var stream = Assembly.GetManifestResourceStream(URL);
        Streams.Add(stream);
        return stream;
    }

    public override Stream GetStream()
    {
        var stream = InternalGetStream();
        if (_MimeType != null) return stream;
        MimeType? mt;
        using (var stream2 = InternalGetStream())
        {
            mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
            Streams.Remove(stream2);
        }
        _MimeType = mt;
        return stream;
    }


    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue) return;
        if (disposing)
            foreach (var stream in Streams.Where(x => x.CanRead))
                stream.Dispose();
        disposedValue = true;
    }
}
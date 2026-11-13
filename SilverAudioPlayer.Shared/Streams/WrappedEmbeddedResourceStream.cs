using System.Reflection;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedEmbeddedResourceStream : WrappedStream
{
    private readonly Assembly Assembly;
    private bool disposedValue;
    private readonly object _lock = new();

    public override bool IsSeekable => true;

    public WrappedEmbeddedResourceStream(Assembly assembly, string resourceLoc)
    {
        Assembly = assembly;
        URL = resourceLoc;
    }

    public string URL { get; set; }
    public List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType => _MimeType!;
    private MimeType? _MimeType { get; set; } = null;

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private Stream InternalGetStream()
    {
        var stream = Assembly.GetManifestResourceStream(URL);
        if (stream == null)
        {
            throw new FileNotFoundException(
                $"Embedded resource '{URL}' was not found in assembly '{Assembly.FullName}'.");
        }

        lock (_lock)
        {
            Streams.Add(stream);
        }

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
            lock (_lock)
            {
                Streams.Remove(stream2);
            }
        }

        _MimeType = mt;
        return stream;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue) return;
        if (disposing)
        {
            lock (_lock)
            {
                foreach (var stream in Streams.Where(x => x.CanRead))
                    stream.Dispose();
                Streams.Clear();
            }
        }

        disposedValue = true;
    }
}
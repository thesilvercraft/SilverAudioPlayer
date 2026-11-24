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
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        URL = resourceLoc ?? throw new ArgumentNullException(nameof(resourceLoc));

        MimeType? mt = null;
        using (var probeStream = Assembly.GetManifestResourceStream(URL))
        {
            if (probeStream != null)
            {
                mt = MagicByteCombos.Match(probeStream, 0)?.MimeType;
            }
        }

        mt ??= KnownMimes.GetKnownMimeByExtension(Path.GetExtension(URL));
        _MimeType = mt ?? KnownMimes.OctetMime;
    }

    public string URL { get; set; }
    public List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private Stream InternalGetStream()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var stream = Assembly.GetManifestResourceStream(URL);
        if (stream == null)
        {
            throw new FileNotFoundException(
                $"Embedded resource '{URL}' was not found in assembly '{Assembly.FullName}'.");
        }

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            Streams.RemoveAll(s => !s.CanRead);
            Streams.Add(stream);
        }

        return stream;
    }

    public override Stream GetStream()
    {
        return InternalGetStream();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue) return;
        if (disposing)
        {
            lock (_lock)
            {
                disposedValue = true;
                foreach (var stream in Streams)
                    stream.Dispose();
                Streams.Clear();
            }
        }
        else
        {
            disposedValue = true;
        }
    }
}
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedFileStream : WrappedRegenerativeStream, IDisposable
{
    private bool disposedValue;

    public WrappedFileStream(string url)
    {
        URL = url;
        MimeType? mt = null;
        var stream2 = InternalGetStream();
        try
        {
            mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
        }
        finally
        {
            stream2.Dispose();
            Streams.Remove(stream2);
        }

        mt ??= KnownMimes.GetKnownMimeByExtension(new FileInfo(URL).Extension);
        MimeType = mt;
    }

    public string URL { get; set; }
    public List<Stream> Streams { get; set; } = new();

    public override MimeType MimeType { get; } = KnownMimes.OctetMime;

    
    private Stream InternalGetStream()
    {
        var Stream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.Read);
        Streams.Add(Stream);
        return Stream;
    }

    public override Stream GetStream()
    {
        var Stream = InternalGetStream();
        return Stream;
    }

    public override bool ShouldDisposeStream => true;
   
    public override void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
            foreach (var stream in Streams.Where(x => x.CanRead))
                stream.Dispose();
    }

    ~WrappedFileStream()
    {
        Dispose(false);
    }
    
}
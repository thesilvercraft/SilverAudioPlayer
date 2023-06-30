using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedHttpStream : WrappedRegenerativeStream, IDisposable
{
    private bool _disposedValue;

    public WrappedHttpStream(string url)
    {
        Url = url;
    }
   
    public string Url { get; set; }
    public List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    private Stream InternalGetStream()
    {
        var content = HttpClient.Client.GetAsync(Url).GetAwaiter().GetResult();
        var Stream = content.Content.ReadAsStream();
        Streams.Add(Stream);
        return Stream;
    }

    public override Stream GetStream()
    {
        var content = HttpClient.Client.GetAsync(Url).GetAwaiter().GetResult();
        var Stream = content.Content.ReadAsStream();
        Streams.Add(Stream);
        if (_MimeType != null) return Stream;
        //KnownMimes.GetKnownMimeByName(content.Content.Headers.ContentType?.MediaType)
        MimeType? mt = null;
        if (mt == null)
        {
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
        }

        _MimeType = mt;

        return Stream;
    }

    public override bool ShouldDisposeStream { get; } = true;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
            foreach (var stream in Streams.Where(x => x.CanRead))
                stream.Dispose();
        _disposedValue = true;
    }
}


public class WrappedSusHttpStream : WrappedRegenerativeStream, IDisposable
{
    private bool _disposedValue;
    private MemoryArrayHolder data = new();

    public WrappedSusHttpStream(string Url) :this(async () => await HttpClient.Client.GetAsync(Url))
    {
        
    }
    public WrappedSusHttpStream( Func<Task<HttpResponseMessage>> wayToSend)
    {
        var content = wayToSend().GetAwaiter().GetResult();
        var Stream = content.Content.ReadAsStream();
        byte[] array;
        if (Stream is MemoryStream m)
        {
            array = m.ToArray();
        }
        else
        {
            using var memstream = new MemoryStream();
            Stream.CopyTo(memstream);
            array = memstream.ToArray();
        }

        data.Bytes = array;
        //KnownMimes.GetKnownMimeByName(content.Content.Headers.ContentType?.MediaType)
        MimeType? mt = null;
        if (mt == null)
        {
            var stream2 =new FakedMemoryStream(data);
            try
            {
                mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
            }
            finally
            {
                stream2.Dispose();
            }
        }
        _MimeType = mt;
    
    }

    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public override Stream GetStream()
    {
        return new FakedMemoryStream(data);
    }

    public override bool ShouldDisposeStream { get; } = true;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
            data = null;
        _disposedValue = true;
    }
}
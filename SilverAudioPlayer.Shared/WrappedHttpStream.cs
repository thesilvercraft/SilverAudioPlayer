using System.Diagnostics;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedHttpStream : WrappedRegenerativeStream, IDisposable
{
    private bool _disposedValue;

    public WrappedHttpStream(string url)
    {
        Url = url;
#if DEBUG
        T = new(Callback, this, 1000, 1000);
        #endif
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
        var content = Task.Run(async () =>
            {
                var stream = await HttpClient.Client.GetAsync(Url);
                return await stream.Content.ReadAsStreamAsync();
            }
        ).GetAwaiter().GetResult();
        Streams.Add(content);
        return content;
    }
#if DEBUG
    private Timer T;

    private void Callback(object? state)
    {
        CheckForReturned();
    }

    public List<Tuple<Stream,string>> NotDisposedStreams = new();
    void CheckForReturned()
    {
        foreach (var sTuple in NotDisposedStreams.ToArray())
        {
            if (sTuple.Item1.CanRead) continue;
            NotDisposedStreams.Remove(sTuple);
            Debug.WriteLine($"{sTuple.Item2} seems to have returned a stream, {NotDisposedStreams.Count} now");
        }
    }
    #endif
    public override Stream GetStream()
    {
        var stream = InternalGetStream();
#if DEBUG
        var mth = new StackTrace().GetFrame(1).GetMethod();
        var cls = mth.ReflectedType.Name;
        CheckForReturned();
        NotDisposedStreams.Add(new(stream,cls));
        Debug.WriteLine($"{cls} borrowed a stream, {NotDisposedStreams.Count} now");
#endif
        if (_MimeType != null) return stream;
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
        return stream;
    }

    public override bool ShouldDisposeStream { get; } = true;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
            foreach (var stream in Streams.Where(x => x.CanRead))
                stream.Dispose();
        #if DEBUG
        T.Dispose();
        #endif
        _disposedValue = true;
    }
}

public class WrappedSusHttpStream : WrappedRegenerativeStream, IDisposable
{
    private bool _disposedValue;
    private MemoryArrayHolder data = new();

    public WrappedSusHttpStream(string Url) : this(async () =>
        await HttpClient.Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead))
    {
    }

    public WrappedSusHttpStream(Func<Task<HttpResponseMessage>> wayToSend)
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
            var stream2 = new FakedMemoryStream(data);
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
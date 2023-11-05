using System.Diagnostics;
using System.Net;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class HttpSeekableStream : Stream
{
    public readonly HttpRequestMessage RequestMessage;

    public HttpSeekableStream(string getURL) : this(new HttpRequestMessage(HttpMethod.Get, getURL))
    {
    }

    public HttpSeekableStream(string URL, ref Dictionary<long, MemoryStream> cachedStreams) : this(URL)
    {
        CachedStreams = cachedStreams;
    }

    public HttpSeekableStream(HttpRequestMessage requestMessage, ref Dictionary<long, MemoryStream> cachedStreams) :
        this(requestMessage)
    {
        CachedStreams = cachedStreams;
    }

    public HttpSeekableStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
        var (a, b) = GetLengthAndRangeSupport(RequestMessage);
        Length = a;
        RangeSupport = b;
        Debug.WriteLine($"RangeSupport {RangeSupport}");
    }

    private bool RangeSupport;
    public Stream? Internal;


    public Dictionary<long, MemoryStream> CachedStreams = new();

    //Values tested 8192, 81920, 512000 
    private const int BuffLength = 512000;

    #region Not supported methods

    public override void Flush()
    {
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    #endregion

    private static HttpRequestMessage CloneMessage(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        using var ms = new MemoryStream();
        if (req.Content != null)
        {
            req.Content.CopyTo(ms, null, default);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.Add(h.Key, h.Value);
        }

        clone.Version = req.Version;
        foreach (var (key, value) in req.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(key), value);
        foreach (var (key, value) in req.Headers)
            clone.Headers.TryAddWithoutValidation(key, value);
        return clone;
    }

    public static Tuple<long, bool> GetLengthAndRangeSupport(HttpRequestMessage RequestMessage)
    {
        var clone = CloneMessage(RequestMessage);
        using var r = HttpClient.Client.Send(clone, HttpCompletionOption.ResponseHeadersRead);
        if (r.IsSuccessStatusCode)
        {
            return new((long)r.Content.Headers.ContentLength, r.Headers.AcceptRanges.Contains("bytes"));
        }

        throw new Exception($"HTTP Status code {r.StatusCode}");
    }

    private Stream GetStream(long pos)
    {
        var clone = CloneMessage(RequestMessage);
        if (RangeSupport)
        {
            clone.Headers.Range = new(pos, pos + BuffLength);
        }

        var r = HttpClient.Client.Send(clone, RangeSupport? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
        if (r.IsSuccessStatusCode)
        {
            return r.Content.ReadAsStream();
        }

        throw new Exception($"HTTP Status code {r.StatusCode}");
    }

    private Tuple<long, long>? ReadStream(long containingByte)
    {
        var streamToRead = (_currentPos / BuffLength);
        var byteToStartAt = streamToRead * BuffLength;
        if (containingByte > Length)
        {
            return null;
        }

        lock (CachedStreams)
        {
            if (CachedStreams.ContainsKey(streamToRead))
            {
                return new(streamToRead, _currentPos % BuffLength);
            }

            using var currentStream = GetStream(byteToStartAt);
            var b = new byte[BuffLength];
            var toRead = BuffLength;
            var offset = 0;
            var read = 0;
            do
            {
                read = currentStream.Read(b, offset, toRead);
                offset += read;
                toRead -= read;
            } while (read > 0);

            MemoryStream stream = new(b, 0, offset, false);
            CachedStreams[streamToRead] = stream;
            return new(streamToRead, _currentPos % BuffLength);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (RangeSupport)
        {
            var totalRead = 0;
            while (count > 0)
            {
                var s = ReadStream(Position);
                if (s == null)
                {
                    //INVALID READ???
                    return totalRead;
                }

                CachedStreams[s.Item1].Position = s.Item2;
                var read = CachedStreams[s.Item1].Read(buffer, offset, count);
                Position += read;
                totalRead += read;
                offset += read;
                count -= read;
                if (read < BuffLength - s.Item2)
                {
                    //assume no more data
                    return totalRead;
                }
            }

            return totalRead;
        }
        else
        {
            Internal ??= GetStream(0);
            var r = Internal.Read(buffer, offset, count);
            _currentPos = Internal.Position;
            return r;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!RangeSupport)
        {
            Internal ??= GetStream(0);
            Internal.Seek(offset, origin);
            _currentPos = Internal.Position;
            return _currentPos;
        }
        switch (origin)
        {
            case SeekOrigin.Begin:
                
                _currentPos = offset;
                break;
            case SeekOrigin.Current:
                _currentPos += offset;
                break;
            case SeekOrigin.End:
                _currentPos = Length - offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return Position;
    }

    public override void Close()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" /> and optionally releases the managed resources.</summary>
    /// <param name="disposing">
    /// <see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        Internal?.Dispose();
        Internal = null;
        // Note: Never change this to call other virtual methods on Stream
        // like Write, since the state on subclasses has already been
        // torn down.  This is the last code to run on cleanup for a stream.
        _isDisposed = true;
    }

    private bool _isDisposed = false;
    public override bool CanRead => !_isDisposed;
    public override bool CanSeek => !_isDisposed && RangeSupport;
    public override bool CanWrite => false;
    public override long Length { get; }
    private long _currentPos;

    public override long Position
    {
        get => _currentPos;
        set => Seek(value, SeekOrigin.Begin);
    }
}
/// <summary>
/// Supports HTTP STREAMS THAT SUPPORT RANGE
/// </summary>
public class WrappedHttpStream : WrappedStream, IDisposable
{
    public WrappedHttpStream(string url) : this(new HttpRequestMessage(HttpMethod.Get,url))
    {
    }
#if DEBUG
    private Timer T;

    private void Callback(object? state)
    {
        CheckForReturned();
    }

    public List<Tuple<Stream, string>> NotDisposedStreams = new();
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
    public WrappedHttpStream(HttpRequestMessage requestMessage)
    {
        inside = new(requestMessage);
        _MimeType = MagicByteCombos.Match(inside, 0)?.MimeType;
#if DEBUG
        T = new(Callback, this, 1000, 1000);
#endif
    }
    private HttpSeekableStream inside;
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public override Stream GetStream()
    {
        var stream = new HttpSeekableStream(inside.RequestMessage, ref inside.CachedStreams);
#if DEBUG
        var mth = new StackTrace().GetFrame(1).GetMethod();
        var cls = mth.ReflectedType.Name;
        CheckForReturned();
        NotDisposedStreams.Add(new(stream, cls));
        Debug.WriteLine($"{cls} borrowed a stream, {NotDisposedStreams.Count} now");
#endif
        return stream;
    }

    public string Url => inside.RequestMessage.RequestUri.ToString();

    public override void Dispose()
    {
        Debug.WriteLine($"WRPHTTPSTRM {inside.RequestMessage} dispose");
        foreach (var s in inside.CachedStreams)
        {
            s.Value.Dispose();
        }
#if DEBUG
        T.Dispose();
#endif
        inside.Dispose();
        GC.SuppressFinalize(this);
    }
}


public class WrappedSusHttpStream : WrappedStream, IDisposable
{
    private bool _disposedValue;
    private MemoryArrayHolder data = new();

    private HttpRequestMessage _requestMessage;

    public WrappedSusHttpStream(HttpRequestMessage requestMessage)
    {
        _requestMessage = requestMessage;
        var content = HttpClient.Client.Send(requestMessage);
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


    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
            data = null;
        _disposedValue = true;
    }
}
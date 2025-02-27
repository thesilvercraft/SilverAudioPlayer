using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class HttpSeekableStream : Stream, IRefreshableStream
{
    public  HttpRequestMessage RequestMessage { get; }

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

    
    private const int BuffLength =  524288;  // 512 KiB
    private const int MaxChunksToBufferAhead = 6; // Have the next 3MiB buffered

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

    public static Tuple<long, bool>? GetLengthAndRangeSupport(HttpRequestMessage RequestMessage)
    {
        var clone = CloneMessage(RequestMessage);
        using var r = HttpClient.Client.Send(clone, HttpCompletionOption.ResponseHeadersRead);
        if (r.IsSuccessStatusCode)
        {
            if (r.Content.Headers.ContentLength is { } length)
            {
                return new(length, r.Headers.AcceptRanges.Contains("bytes"));
            }
            return null;
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

    private ConcurrentDictionary<long, Task> BeingDownloaded=new();
    
    Task ReadChunk(long chunkNum, CancellationToken token=default, bool au=false)
    {

        if (!au && BeingDownloaded.TryGetValue(chunkNum, out var chunk))
        {
            if (chunk.Status == TaskStatus.RanToCompletion)
            {
                BeingDownloaded.Remove(chunkNum, out _);
            }
            return chunk;
        }
        void SeeBufferNext()
        {
            if (au) return;
            
            if(chunkNum * BuffLength > Length) return;
           // Debug.WriteLine($"We are currently at {(_currentPos) / 1000}K aka {_currentPos / BuffLength}");
            for (int i = 1; i <= MaxChunksToBufferAhead; i++)
            {
                if (BeingDownloaded.ContainsKey(chunkNum + i) || CachedStreams.ContainsKey(chunkNum+i)) continue;
                //Debug.WriteLine($"{((chunkNum+ 1)*BuffLength)/1000}K aka {chunkNum+i} is {Math.Abs((chunkNum+ i) * BuffLength-_currentPos) / BuffLength} chunks ahead");
                if (Math.Abs((chunkNum+ i) * BuffLength-_currentPos) / BuffLength >= MaxChunksToBufferAhead) return;
                //number of chunks <= next 
                if ((Length / BuffLength) - 1 < chunkNum + i) return;
                Debug.WriteLine($"adding chunk number {chunkNum+i}");
                BeingDownloaded[chunkNum + i]=  Task.Run(() =>
                {
                    ReadChunk(chunkNum + i, token,true);
                });
                break;
            }
       
               
        }
        if (CachedStreams.ContainsKey(chunkNum))
        {
            SeeBufferNext();
            return Task.CompletedTask;
        }
        Debug.WriteLine($"Downloading chunk number {chunkNum}");
        var byteToStartAt = chunkNum * BuffLength;
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
        CachedStreams[chunkNum] = stream;
        SeeBufferNext();
        return Task.CompletedTask;

    }
    private Tuple<long, long>? ReadStream(long containingByte)
    {
        var streamToRead = (_currentPos / BuffLength);
        if (containingByte > Length)
        {
            return null;
        }

        lock (CachedStreams)
        {
            ReadChunk(streamToRead, default).GetAwaiter().GetResult();
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
                _currentPos = Length - Math.Abs( offset);
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
        Internal = null;
        if (Inside)
        {
            Internal?.Dispose();
            foreach (var s in CachedStreams.ToList())
            {
                s.Value.Dispose();
            }
            CachedStreams.Clear();
        }
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
    public bool Inside;

    public override long Position
    {
        get => _currentPos;
        set => Seek(value, SeekOrigin.Begin);
    }
    
    public Stream GetStream()
    {
        return new HttpSeekableStream(RequestMessage, ref CachedStreams);
    }
}

public interface IRefreshableStream  
{
    public  HttpRequestMessage RequestMessage { get; }
    public Stream GetStream();
    public void Dispose();
}
/// <summary>
/// Supports HTTP STREAMS THAT SUPPORT RANGE
/// </summary>
public class WrappedHttpStream : WrappedStream, IDisposable
{
    public WrappedHttpStream(string url) : this(new HttpRequestMessage(HttpMethod.Get,url))
    {
    }
    public bool Seekable { get; }=false;
    public WrappedHttpStream(HttpRequestMessage requestMessage)
    {
        var stype = HttpSeekableStream.GetLengthAndRangeSupport(requestMessage);
        if (stype is {Item2:true})
        {
            inside = new HttpSeekableStream(requestMessage){Inside=true};
            Seekable = true;
        }
        else if(stype== null)
        {
            inside = new NoEndHttpStream(requestMessage);
        }
        else
        {
            inside= new WrappedSusHttpStream(requestMessage);
        }

        using var s = inside.GetStream();
        _MimeType = MagicByteCombos.Match(s, 0)?.MimeType;

    }

   
    private IRefreshableStream inside;
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public override Stream GetStream()
    {
        var stream = inside.GetStream();
        return stream;
    }

    public string Url => inside.RequestMessage.RequestUri.ToString();

    public override void Dispose()
    {
        Debug.WriteLine($"WRPHTTPSTRM {inside.RequestMessage} dispose");
        inside.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class NoEndHttpStream : IRefreshableStream, IDisposable
{
    private bool _disposedValue;


    public NoEndHttpStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public HttpRequestMessage RequestMessage { get; }

    public Stream GetStream()
    {
        return (HttpClient.Client.Send(new HttpRequestMessage(RequestMessage.Method, RequestMessage.RequestUri), HttpCompletionOption.ResponseHeadersRead).Content.ReadAsStream());
    }


    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
    }
}



public class WrappedSusHttpStream : IRefreshableStream, IDisposable
{
    private bool _disposedValue;
    private MemoryArrayHolder data = new();


    public WrappedSusHttpStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
        var content = HttpClient.Client.Send(requestMessage);
        using var stream = content.Content.ReadAsStream();
        byte[] array;
        if (stream is MemoryStream m)
        {
            array = m.ToArray();
        }
        else
        {
            using var memstream = new MemoryStream();
            stream.CopyTo(memstream);
            array = memstream.ToArray();
        }
        data.Bytes = array;
      
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public HttpRequestMessage RequestMessage { get; }

    public Stream GetStream()
    {
        return new FakedMemoryStream(data);
    }


    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            data.Bytes = null;
            data = null;
        }
        _disposedValue = true;
    }
}
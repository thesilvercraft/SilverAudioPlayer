using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;

namespace SilverAudioPlayer.Shared;

#if DEBUG
public static class HttpSeekableStreamCounter
{
    private static readonly Meter s_meter = new("SilverCraft.SilverAudioPlayer.HttpSeekableStream", "1.0.0");
    public static Counter<long> BytesDownloaded = s_meter.CreateCounter<long>("KB downloaded");
}
#endif

public class HttpSeekableStream : Stream, IRefreshableStream
{
    private const int BuffLength = 524288; // 512 KiB
    private const int MaxChunksToBufferAhead = 6; // Prefetch up to 3MiB ahead
    
    private readonly ConcurrentDictionary<long, Task<Memory<byte>>> _chunkCache;
    private readonly bool _rangeSupport;
    private long _currentPos;
    private bool _isDisposed;
    
    private HttpResponseMessage? _iMessage;
    private Stream? _internalStream;
    public bool IsParent { get; set; }

    public HttpSeekableStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
        _chunkCache = new ConcurrentDictionary<long, Task<Memory<byte>>>();
        var result = GetLengthAndRangeSupport(RequestMessage);
        if (result != null)
        {
            Length = result.Item1;
            _rangeSupport = result.Item2;
        }
    }

    public HttpSeekableStream(string getURL) : this(new HttpRequestMessage(HttpMethod.Get, getURL))
    {
    }

    public HttpSeekableStream(HttpSeekableStream parent)
    {
        _chunkCache = parent._chunkCache;
        RequestMessage = parent.RequestMessage;
        _rangeSupport = parent._rangeSupport;
        Length = parent.Length;
    }

    public override bool CanRead => !_isDisposed;
    public override bool CanSeek => !_isDisposed && _rangeSupport;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => _currentPos;
        set => Seek(value, SeekOrigin.Begin);
    }

    public HttpRequestMessage RequestMessage { get; }

    public Stream GetStream() => new HttpSeekableStream(this);

    private static HttpRequestMessage CloneMessage(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        foreach (var (key, value) in req.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(key), value);

        if (req.Content != null)
        {
            lock (req)
            {
                using var ms = new MemoryStream();
                req.Content.CopyTo(ms, null, default);
                ms.Position = 0;
                clone.Content = new StreamContent(new MemoryStream(ms.ToArray()));
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.Add(h.Key, h.Value);
            }
        }

        return clone;
    }

    public static Tuple<long, bool>? GetLengthAndRangeSupport(HttpRequestMessage requestMessage)
    {
        using var clone = CloneMessage(requestMessage);
        using var r = HttpClient.Client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        if (r.IsSuccessStatusCode)
        {
            if (r.Content.Headers.ContentLength is { } length)
                return new Tuple<long, bool>(length, r.Headers.AcceptRanges.Contains("bytes"));
            return null;
        }

        throw new Exception($"HTTP Status code {r.StatusCode}");
    }

    private async Task<Memory<byte>> GetChunkAsync(long chunkNum, CancellationToken cancellationToken = default)
    {
        var byteToStartAt = chunkNum * BuffLength;
        if (byteToStartAt >= Length) 
            return Memory<byte>.Empty;

        using var clone = CloneMessage(RequestMessage);
        if (_rangeSupport)
            clone.Headers.Range = new RangeHeaderValue(byteToStartAt, byteToStartAt + BuffLength - 1);

        using var response = await HttpClient.Client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP Status code {response.StatusCode} fetching chunk {chunkNum}");

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        
        var buffer = new byte[BuffLength];
        var totalRead = 0;
        int read;

        while (totalRead < BuffLength && (read = await stream.ReadAsync(buffer.AsMemory(totalRead, BuffLength - totalRead), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += read;
        }

#if DEBUG
        HttpSeekableStreamCounter.BytesDownloaded.Add(totalRead / 1024);
#endif

        return new Memory<byte>(buffer, 0, totalRead);
    }

    private Memory<byte> GetOrDownloadChunk(long chunkNum)
    {
        var downloadTask = _chunkCache.GetOrAdd(chunkNum, id => GetChunkAsync(id));
        
        TriggerPrefetch(chunkNum);

        return downloadTask.GetAwaiter().GetResult();
    }

    private void TriggerPrefetch(long currentChunkNum)
    {
        if (Length <= 0) return;
        var maxChunk = (Length + BuffLength - 1) / BuffLength;

        for (var i = 1; i <= MaxChunksToBufferAhead; i++)
        {
            var nextChunk = currentChunkNum + i;
            if (nextChunk >= maxChunk) break;

            // Fire and forget downloading task into the Cache cleanly without locks
            _ = _chunkCache.GetOrAdd(nextChunk, id => GetChunkAsync(id));
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_rangeSupport)
        {
            if (_currentPos >= Length)
                return 0;

            var totalRead = 0;
            while (count > 0 && _currentPos < Length)
            {
                var chunkId = _currentPos / BuffLength;
                var location = _currentPos % BuffLength;

                var chunk = GetOrDownloadChunk(chunkId);
                if (chunk.IsEmpty)
                    break;

                var bytesAvailableInChunk = chunk.Length - (int)location;
                if (bytesAvailableInChunk <= 0)
                    break;

                var bytesToRead = Math.Min(bytesAvailableInChunk, count);
                chunk.Span.Slice((int)location, bytesToRead).CopyTo(buffer.AsSpan(offset, bytesToRead));

                _currentPos += bytesToRead;
                totalRead += bytesToRead;
                offset += bytesToRead;
                count -= bytesToRead;
            }

            return totalRead;
        }

        if (_internalStream == null)
        {
            using var clone = CloneMessage(RequestMessage);
            var r = HttpClient.Client.Send(clone, HttpCompletionOption.ResponseContentRead);
            _iMessage = r;
            _internalStream = r.Content.ReadAsStream();
        }

        var bytesRead = _internalStream.Read(buffer, offset, count);
        _currentPos = _internalStream.Position;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!_rangeSupport)
        {
            if (_internalStream == null)
            {
                using var clone = CloneMessage(RequestMessage);
                var r = HttpClient.Client.Send(clone, HttpCompletionOption.ResponseContentRead);
                _iMessage = r;
                _internalStream = r.Content.ReadAsStream();
            }

            _internalStream.Seek(offset, origin);
            _currentPos = _internalStream.Position;
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
                _currentPos = Length - Math.Abs(offset);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return _currentPos;
    }

    protected override void Dispose(bool disposing)
    {
        Debug.WriteLine("Disposing HTTP stream");
        if (!_isDisposed && disposing)
        {
            if (IsParent)
            {
                Debug.WriteLine("Disposing Parent http stream");
                RequestMessage.Dispose();
                _chunkCache.Clear();
            }

            _internalStream?.Dispose();
            _internalStream = null;
            _iMessage?.Dispose();
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
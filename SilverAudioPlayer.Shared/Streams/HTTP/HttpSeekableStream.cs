using System.Collections.Concurrent;
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
    private readonly object _downloadLock = new();

    private readonly ConcurrentDictionary<long, Task> BeingDownloaded = new();

    private readonly bool RangeSupport;
    private long _currentPos;
    private bool _isDisposed;
    public ConcurrentDictionary<long, Memory<byte>> CachedStreams = new();
    private HttpResponseMessage? IMessage;

    public Stream? Internal;
    public bool IsParent;

    public HttpSeekableStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
        var result = GetLengthAndRangeSupport(RequestMessage);
        if (result != null)
        {
            Length = result.Item1;
            RangeSupport = result.Item2;
        }
    }

    public HttpSeekableStream(string getURL) : this(new HttpRequestMessage(HttpMethod.Get, getURL))
    {
    }

    public HttpSeekableStream(HttpSeekableStream parent)
    {
        CachedStreams = parent.CachedStreams;
        BeingDownloaded = parent.BeingDownloaded;
        RequestMessage = parent.RequestMessage;
        RangeSupport = parent.RangeSupport;
        Length = parent.Length;
    }

    public override bool CanRead => !_isDisposed;
    public override bool CanSeek => !_isDisposed && RangeSupport;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => _currentPos;
        set => Seek(value, SeekOrigin.Begin);
    }

    public HttpRequestMessage RequestMessage { get; }

    public Stream GetStream()
    {
        return new HttpSeekableStream(this);
    }

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
            lock (req)
            {
                using var ms = new MemoryStream();
                req.Content.CopyTo(ms, null, default);
                ms.Position = 0;
                clone.Content = new StreamContent(new MemoryStream(ms.ToArray()));
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.Add(h.Key, h.Value);
            }

        return clone;
    }

    public static Tuple<long, bool>? GetLengthAndRangeSupport(HttpRequestMessage requestMessage)
    {
        using var clone = CloneMessage(requestMessage);
        using var r = HttpClient.Client.Send(clone, HttpCompletionOption.ResponseHeadersRead);
        if (r.IsSuccessStatusCode)
        {
            if (r.Content.Headers.ContentLength is { } length)
                return new Tuple<long, bool>(length, r.Headers.AcceptRanges.Contains("bytes"));
            return null;
        }

        throw new Exception($"HTTP Status code {r.StatusCode}");
    }

    private (HttpResponseMessage, Stream) GetNewStreamAt(long pos)
    {
        var clone = CloneMessage(RequestMessage);
        if (RangeSupport)
            clone.Headers.Range = new RangeHeaderValue(pos, pos + BuffLength - 1);

        var r = HttpClient.Client.Send(clone,
            RangeSupport ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
        return r.IsSuccessStatusCode
            ? (r, r.Content.ReadAsStream())
            : throw new Exception($"HTTP Status code {r.StatusCode}");
    }

    private void EnsureChunkAvailable(long chunkNum)
    {
        if (CachedStreams.ContainsKey(chunkNum))
        {
            TriggerPrefetch(chunkNum);
            return;
        }

        Task? backgroundTask;
        lock (_downloadLock)
        {
            if (CachedStreams.ContainsKey(chunkNum)) return;
            BeingDownloaded.TryGetValue(chunkNum, out backgroundTask);
        }

        if (backgroundTask != null)
            backgroundTask.GetAwaiter().GetResult();
        else
            DownloadChunkInternal(chunkNum);

        TriggerPrefetch(chunkNum);
    }

    private void DownloadChunkInternal(long chunkNum)
    {
        if (CachedStreams.ContainsKey(chunkNum)) return;

        var byteToStartAt = chunkNum * BuffLength;
        if (byteToStartAt >= Length) return;


        var (hM, stream) = GetNewStreamAt(byteToStartAt);
        using (hM)
        using (stream)
        {
            var b = new byte[BuffLength];
            var toRead = BuffLength;
            var offset = 0;
            int read;

            while (toRead > 0 && (read = stream.Read(b, offset, toRead)) > 0)
            {
                offset += read;
                toRead -= read;
            }
#if DEBUG
            HttpSeekableStreamCounter.BytesDownloaded.Add(offset / 1024);
#endif
            if (offset > 0) CachedStreams[chunkNum] = new Memory<byte>(b, 0, offset);
        }
    }

    private void TriggerPrefetch(long currentChunkNum)
    {
        if (Length <= 0) return;
        var maxChunk = (Length + BuffLength - 1) / BuffLength;

        for (var i = 1; i <= MaxChunksToBufferAhead; i++)
        {
            var nextChunk = currentChunkNum + i;
            if (nextChunk >= maxChunk) break;

            lock (_downloadLock)
            {
                if (CachedStreams.ContainsKey(nextChunk) || BeingDownloaded.ContainsKey(nextChunk))
                    continue;

                var chunkToDownload = nextChunk;
                BeingDownloaded[chunkToDownload] = Task.Run(() =>
                {
                    try
                    {
                        DownloadChunkInternal(chunkToDownload);
                    }
                    finally
                    {
                        lock (_downloadLock)
                        {
                            BeingDownloaded.TryRemove(chunkToDownload, out _);
                        }
                    }
                });
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (RangeSupport)
        {
            if (_currentPos >= Length)
                return 0; // Standard EOF protection

            var totalRead = 0;
            while (count > 0 && _currentPos < Length)
            {
                var chunkId = _currentPos / BuffLength;
                var location = _currentPos % BuffLength;

                EnsureChunkAvailable(chunkId);

                if (!CachedStreams.TryGetValue(chunkId, out var chunk))
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

        if (Internal == null)
        {
            var (hM, stream) = GetNewStreamAt(0);
            IMessage = hM;
            Internal = stream;
        }

        var r = Internal.Read(buffer, offset, count);
        _currentPos = Internal.Position;
        return r;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!RangeSupport)
        {
            if (Internal == null)
            {
                var (hM, stream) = GetNewStreamAt(0);
                IMessage = hM;
                Internal = stream;
            }

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
                _currentPos = Length - Math.Abs(offset);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return _currentPos;
    }

    public override void Close()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (IsParent)
            {
                RequestMessage.Dispose();
                CachedStreams.Clear();
            }

            Internal?.Dispose();
            Internal = null;
            IMessage?.Dispose();
        }

        _isDisposed = true;
    }

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
}
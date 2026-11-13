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
    // Modest chunk size (64 KiB) for fast startup and low seek latency.
    public const int BuffLength = 65536; // 64 KiB

    // Multi-chunk prefetch depth to avoid network latency underruns.
    public const int MaxChunksToBufferAhead = 8;

    private readonly ConcurrentDictionary<long, Task<Memory<byte>>> _chunkCache;
    private readonly bool _rangeSupport;
    private readonly bool _hasKnownLength;
    private long _currentPos;
    private bool _isDisposed;
    private readonly CancellationTokenSource _cts;
    private readonly bool _ownsCts;

    private HttpResponseMessage? _iMessage;
    private Stream? _internalStream;
    public bool IsParent { get; set; }
    private HttpClient _client;

    public HttpSeekableStream(HttpRequestMessage requestMessage, HttpClient client)
        : this(requestMessage, client, GetLengthAndRangeSupport(requestMessage, client))
    {
    }

    public HttpSeekableStream(HttpRequestMessage requestMessage, HttpClient client, Tuple<long, bool>? probeResult)
        : this(requestMessage, client,
            probeResult != null ? new ProbeResult(probeResult.Item1, probeResult.Item2) : null)
    {
    }

    public HttpSeekableStream(HttpRequestMessage requestMessage, HttpClient client, ProbeResult? probeResult)
    {
        RequestMessage = requestMessage;
        _client = client;
        _cts = new CancellationTokenSource();
        _ownsCts = true;
        _chunkCache = new ConcurrentDictionary<long, Task<Memory<byte>>>();
        if (probeResult != null)
        {
            Length = probeResult.Length;
            _rangeSupport = probeResult.SupportsRange;
            _hasKnownLength = true;

            if (probeResult.InitialChunk.HasValue)
            {
                _chunkCache[0] = Task.FromResult(probeResult.InitialChunk.Value);
            }
        }
    }

    public HttpSeekableStream(string getURL, HttpClient client) : this(new HttpRequestMessage(HttpMethod.Get, getURL),
        client)
    {
    }

    public HttpSeekableStream(HttpSeekableStream parent)
    {
        _client = parent._client;
        _chunkCache = parent._chunkCache;
        _cts = parent._cts;
        _ownsCts = false;
        RequestMessage = parent.RequestMessage;
        _rangeSupport = parent._rangeSupport;
        _hasKnownLength = parent._hasKnownLength;
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

    /// <summary>True when every byte of the known-length response is already present in the chunk cache.</summary>
    public bool IsFullyDownloaded
    {
        get
        {
            if (!_hasKnownLength) return false;
            if (Length == 0) return true;

            var chunkCount = (Length + BuffLength - 1) / BuffLength;
            for (long chunk = 0; chunk < chunkCount; chunk++)
            {
                if (!_chunkCache.TryGetValue(chunk, out var task) || !task.IsCompletedSuccessfully)
                    return false;

                var expectedLength = (int)Math.Min(BuffLength, Length - chunk * BuffLength);
                if (task.Result.Length != expectedLength) return false;
            }

            return true;
        }
    }

    /// <summary>Copies only already-downloaded bytes to a file; this never starts or waits for an HTTP request.</summary>
    public async Task SaveFullyDownloadedAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsFullyDownloaded)
            throw new InvalidOperationException("The HTTP stream has not been fully downloaded.");

        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            BuffLength, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var chunkCount = (Length + BuffLength - 1) / BuffLength;
        for (long chunk = 0; chunk < chunkCount; chunk++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = await _chunkCache[chunk].ConfigureAwait(false);
            await output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    public Stream GetStream() => new HttpSeekableStream(this);

    public static HttpRequestMessage CloneMessage(HttpRequestMessage req)
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

    public static ProbeResult? GetLengthAndRangeSupport(HttpRequestMessage requestMessage, HttpClient client)
    {
        using var clone = CloneMessage(requestMessage);
        // Probe initial chunk (0 to BuffLength - 1) to both verify range support, get total length,
        // and optionally seed chunk 0, eliminating an entire RTT.
        clone.Headers.Range = new RangeHeaderValue(0, BuffLength - 1);
        using var r = client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        if (r.IsSuccessStatusCode)
        {
            if (r.StatusCode == System.Net.HttpStatusCode.PartialContent &&
                r.Content.Headers.ContentRange is { Unit: "bytes", From: 0, Length: { } totalLength } cr)
            {
                var to = cr.To ?? (BuffLength - 1);
                var expectedLen = (int)Math.Min(totalLength, to - 0 + 1);
                var initialBytes = r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var chunk0 = new Memory<byte>(initialBytes, 0, Math.Min(initialBytes.Length, expectedLen));
                return new ProbeResult(totalLength, true, chunk0);
            }

            if (r.StatusCode == System.Net.HttpStatusCode.OK && r.Content.Headers.ContentLength is { } length)
            {
                return new ProbeResult(length, false);
            }

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

        using var response = await _client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP Status code {response.StatusCode} fetching chunk {chunkNum}");

        var expectedLength = (int)Math.Min(BuffLength, Length - byteToStartAt);
        if (_rangeSupport)
        {
            var expectedEnd = byteToStartAt + expectedLength - 1;
            if (response.StatusCode != System.Net.HttpStatusCode.PartialContent ||
                response.Content.Headers.ContentRange is not
                    { Unit: "bytes", From: var rangeStart, To: var rangeEnd, Length: var totalLength } ||
                rangeStart != byteToStartAt || rangeEnd != expectedEnd || totalLength != Length)
            {
                throw new InvalidDataException(
                    $"Server returned an invalid byte range for chunk {chunkNum} (requested {byteToStartAt}-{expectedEnd}, got {response.StatusCode} {response.Content.Headers.ContentRange}).");
            }
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[BuffLength];
        var totalRead = 0;
        int read;

        while (totalRead < expectedLength && (read = await stream
                   .ReadAsync(buffer.AsMemory(totalRead, expectedLength - totalRead), cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            totalRead += read;
        }

        if (_rangeSupport && totalRead != expectedLength)
        {
            throw new EndOfStreamException(
                $"Server returned {totalRead} bytes for chunk {chunkNum}; expected {expectedLength}.");
        }

#if DEBUG
        HttpSeekableStreamCounter.BytesDownloaded.Add(totalRead / 1024);
#endif

        return new Memory<byte>(buffer, 0, totalRead);
    }

    private Memory<byte> GetOrDownloadChunk(long chunkNum)
    {
        var token = _cts.Token;
        var downloadTask = _chunkCache.GetOrAdd(chunkNum, id => CreateMonitoredChunkTask(id, token));

        TriggerPrefetch(chunkNum);

        try
        {
            return downloadTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Evict faulted task to avoid cache poisoning on transient errors
            _chunkCache.TryRemove(new KeyValuePair<long, Task<Memory<byte>>>(chunkNum, downloadTask));
            throw;
        }
    }

    private Task<Memory<byte>> CreateMonitoredChunkTask(long chunkNum, CancellationToken token)
    {
        var task = GetChunkAsync(chunkNum, token);
        task.ContinueWith(t =>
        {
            if (t.IsFaulted || t.IsCanceled)
            {
                _chunkCache.TryRemove(new KeyValuePair<long, Task<Memory<byte>>>(chunkNum, task));
            }
        }, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
        return task;
    }

    private void TriggerPrefetch(long currentChunkNum)
    {
        if (Length <= 0) return;
        var maxChunk = (Length + BuffLength - 1) / BuffLength;
        var token = _cts.Token;

        for (var i = 1; i <= MaxChunksToBufferAhead; i++)
        {
            var nextChunk = currentChunkNum + i;
            if (nextChunk >= maxChunk) break;

            _ = _chunkCache.GetOrAdd(nextChunk, id => CreateMonitoredChunkTask(id, token));
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count)
            throw new ArgumentException("Offset and length were out of bounds for the array.");

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
            var r = _client.Send(clone, HttpCompletionOption.ResponseContentRead);
            _iMessage = r;
            _internalStream = r.Content.ReadAsStream();
        }

        var bytesRead = _internalStream.Read(buffer, offset, count);
        _currentPos += bytesRead;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!_rangeSupport)
        {
            throw new NotSupportedException(
                "Seeking is not supported on this HTTP stream because the server does not support byte ranges.");
        }

        long targetPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _currentPos + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };

        if (targetPos < 0)
        {
            throw new IOException(
                $"An attempt was made to move the position before the beginning of the stream. Target position: {targetPos}");
        }

        _currentPos = targetPos;
        return _currentPos;
    }

    protected override void Dispose(bool disposing)
    {
        Debug.WriteLine("Disposing HTTP stream");
        if (!_isDisposed && disposing)
        {
            if (_ownsCts || IsParent)
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch
                {
                    // Ignore cancel exceptions during dispose
                }
            }

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

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
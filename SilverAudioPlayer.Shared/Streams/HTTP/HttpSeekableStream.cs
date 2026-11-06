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
    // Keep the first blocking read small. Decoder and metadata readers often only
    // need a few kilobytes before they can identify the file or start decoding.
    private const int BuffLength = 65536; // 64 KiB
    private const int MaxChunksToBufferAhead = 1;

    private readonly ConcurrentDictionary<long, Task<Memory<byte>>> _chunkCache;
    private readonly bool _rangeSupport;
    private readonly bool _hasKnownLength;
    private long _currentPos;
    private bool _isDisposed;

    private HttpResponseMessage? _iMessage;
    private Stream? _internalStream;
    public bool IsParent { get; set; }
    private HttpClient _client;

    public HttpSeekableStream(HttpRequestMessage requestMessage, HttpClient client)
        : this(requestMessage, client, GetLengthAndRangeSupport(requestMessage, client))
    {
    }

    internal HttpSeekableStream(HttpRequestMessage requestMessage, HttpClient client, Tuple<long, bool>? probeResult)
    {
        RequestMessage = requestMessage;
        _client = client;
        _chunkCache = new ConcurrentDictionary<long, Task<Memory<byte>>>();
        var result = probeResult;
        if (result != null)
        {
            Length = result.Item1;
            _rangeSupport = result.Item2;
            _hasKnownLength = true;
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

    public static Tuple<long, bool>? GetLengthAndRangeSupport(HttpRequestMessage requestMessage, HttpClient client)
    {
        using var clone = CloneMessage(requestMessage);
        // Accept-Ranges is advisory. Probe an actual range so servers that advertise
        // support but ignore Range do not get treated as seekable.
        clone.Headers.Range = new RangeHeaderValue(0, 0);
        using var r = client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        if (r.IsSuccessStatusCode)
        {
            return r.StatusCode switch
            {
                System.Net.HttpStatusCode.PartialContent when r.Content.Headers.ContentRange is
                {
                    Unit: "bytes", From: 0, To: 0, Length: { } totalLength
                } => new Tuple<long, bool>(totalLength, true),
                System.Net.HttpStatusCode.OK when r.Content.Headers.ContentLength is { } length =>
                    new Tuple<long, bool>(length, false),
                _ => null
            };
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
            var r = _client.Send(clone, HttpCompletionOption.ResponseContentRead);
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
                var r = _client.Send(clone, HttpCompletionOption.ResponseContentRead);
                _iMessage = r;
                _internalStream = r.Content.ReadAsStream();
            }

            _internalStream.Seek(offset, origin);
            _currentPos = _internalStream.Position;
            return _currentPos;
        }

        //  Log.Debug("Seek in HTTPSeekableStream {Offset} {Origin}", offset, origin);
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

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
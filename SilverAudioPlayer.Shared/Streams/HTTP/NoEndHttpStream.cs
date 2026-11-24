namespace SilverAudioPlayer.Shared;

public class NoEndHttpStream(HttpRequestMessage requestMessage, HttpClient client) : IRefreshableStream, IDisposable
{
    private bool _disposedValue;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public HttpRequestMessage RequestMessage { get; } = requestMessage;

    public Stream GetStream()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        var clone = HttpSeekableStream.CloneMessage(RequestMessage);
        HttpResponseMessage? response = null;
        try
        {
            response = client.SendAsync(clone, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            var inner = response.Content.ReadAsStream();
            return new HttpResponseOwnedStream(inner, response, clone);
        }
        catch
        {
            response?.Dispose();
            clone.Dispose();
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            RequestMessage.Dispose();
        }

        _disposedValue = true;
    }

    private sealed class HttpResponseOwnedStream(Stream inner, HttpResponseMessage response, HttpRequestMessage request)
        : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
                request.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
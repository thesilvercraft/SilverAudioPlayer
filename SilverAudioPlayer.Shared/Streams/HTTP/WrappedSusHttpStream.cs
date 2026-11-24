namespace SilverAudioPlayer.Shared;

public class WrappedSusHttpStream : IRefreshableStream, IDisposable
{
    private SharedBuffer? _sharedBuffer;
    private bool _isDisposed;

    public HttpRequestMessage RequestMessage { get; }

    public WrappedSusHttpStream(HttpRequestMessage requestMessage, HttpClient client)
    {
        RequestMessage = requestMessage;

        using var clone = HttpSeekableStream.CloneMessage(requestMessage);
        using var response = client.SendAsync(clone, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        _sharedBuffer = new SharedBuffer(bytes);
    }

    public Stream GetStream()
    {
        if (_isDisposed || _sharedBuffer == null)
            throw new ObjectDisposedException(nameof(WrappedSusHttpStream));

        return new SharedMemoryStream(_sharedBuffer);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _sharedBuffer?.Release();
            _sharedBuffer = null;
            RequestMessage.Dispose();
            _isDisposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
namespace SilverAudioPlayer.Shared;

public class WrappedSusHttpStream : IRefreshableStream, IDisposable
{
    private SharedBuffer? _sharedBuffer;
    private bool _isDisposed;

    public HttpRequestMessage RequestMessage { get; }

    public WrappedSusHttpStream(HttpRequestMessage requestMessage)
    {
        RequestMessage = requestMessage;
        
        using var response = HttpClient.Client.Send(requestMessage, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();

        using var responseStream = response.Content.ReadAsStream();
        
        if (responseStream is MemoryStream standardMemoryStream)
        {
            _sharedBuffer = new SharedBuffer(standardMemoryStream.ToArray());
        }
        else
        {
            using var targetMemoryStream = new MemoryStream();
            responseStream.CopyTo(targetMemoryStream);
            _sharedBuffer = new SharedBuffer(targetMemoryStream.ToArray());
        }
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

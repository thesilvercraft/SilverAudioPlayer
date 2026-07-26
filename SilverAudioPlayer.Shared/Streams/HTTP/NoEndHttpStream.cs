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
        return client
            .Send(new HttpRequestMessage(RequestMessage.Method, RequestMessage.RequestUri),
                HttpCompletionOption.ResponseHeadersRead).Content.ReadAsStream();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
    }
}
namespace SilverAudioPlayer.Shared;

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
        return HttpClient.Client
            .Send(new HttpRequestMessage(RequestMessage.Method, RequestMessage.RequestUri),
                HttpCompletionOption.ResponseHeadersRead).Content.ReadAsStream();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
    }
}
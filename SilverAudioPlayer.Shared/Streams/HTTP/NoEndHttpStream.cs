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
        var clone = HttpSeekableStream.CloneMessage(RequestMessage);
        return client
            .Send(clone, HttpCompletionOption.ResponseHeadersRead)
            .Content.ReadAsStream();
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
}
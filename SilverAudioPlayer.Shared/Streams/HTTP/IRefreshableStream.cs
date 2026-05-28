namespace SilverAudioPlayer.Shared;

public interface IRefreshableStream
{
    public HttpRequestMessage RequestMessage { get; }
    public Stream GetStream();
    public void Dispose();
}
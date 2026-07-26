namespace SilverAudioPlayer.Shared;

public interface IMediator
{
    void SendCommand(IMessage message);
    TResponse? RequestQuery<TResponse>(IQuery<TResponse> query);
    event Action<QueryEventArgs>? OnQueryReceived;
    event EventHandler<IMessage>? OnMessageReceived;
}
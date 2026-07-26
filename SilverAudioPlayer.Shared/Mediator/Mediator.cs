namespace SilverAudioPlayer.Shared;

public class Mediator : IMediator
{
    public event Action<QueryEventArgs>? OnQueryReceived;
    public event EventHandler<IMessage>? OnMessageReceived;

    /// <summary>
    /// Sends a command message to the mediator, triggering any registered message handlers.
    /// </summary>
    /// <param name="message">The message command that needs to be sent.</param>
    public void SendCommand(IMessage message)
    {
        OnMessageReceived?.Invoke(this, message);
    }

    /// <summary>
    /// Requests a specific query response from the mediator system.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type for the query.</typeparam>
    /// <param name="query">The query object containing the request data.</param>
    /// <return>A nullable instance of <typeparamref name="TResponse"/> representing the result, or null if no handler processed the request or an error occurred.</return>
    public TResponse? RequestQuery<TResponse>(IQuery<TResponse> query)
    {
        if (OnQueryReceived == null)
        {
            return default;
        }

        var args = new QueryEventArgs(query);

        foreach (var delegateHandler in OnQueryReceived.GetInvocationList())
        {
            var handler = (Action<QueryEventArgs>)delegateHandler;
            
            handler(args);

            if (args.Handled)
            {
                return (TResponse?)args.Result;
            }
        }

        return default;
    }
}
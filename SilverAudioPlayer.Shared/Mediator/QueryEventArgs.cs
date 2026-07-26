namespace SilverAudioPlayer.Shared;

public class QueryEventArgs
{
    public IMessage Query { get; }
    public object? Result { get; set; }
    public bool Handled { get; set; }

    public QueryEventArgs(IMessage query)
    {
        Query = query;
    }
}
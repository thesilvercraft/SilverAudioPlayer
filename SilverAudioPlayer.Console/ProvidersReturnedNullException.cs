namespace SilverAudioPlayer.Console;

[Serializable]
public class ProvidersReturnedNullException : Exception
{
    public ProvidersReturnedNullException()
    {
    }

    public ProvidersReturnedNullException(string message) : base(message)
    {
    }

    public ProvidersReturnedNullException(string message, Exception inner) : base(message, inner)
    {
    }

}
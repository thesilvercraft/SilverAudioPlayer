namespace SilverAudioPlayer.Shared;

public interface IPlayStreamProvider : IUsablePlugin<IPlayStreamProviderListener> 
{
}

public interface IPlayStreamProviderThatSupportsUrls:IPlayStreamProvider
{
    bool IsUrlSupported(Uri given, IPlayStreamProviderListener listener);
    Task LoadUrlAsync(Uri given,IPlayStreamProviderListener listener);
}
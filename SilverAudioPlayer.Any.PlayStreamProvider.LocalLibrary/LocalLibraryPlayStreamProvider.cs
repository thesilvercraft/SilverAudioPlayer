using System.Composition;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;

[Export(typeof(IPlayStreamProvider))]
public class LocalLibraryPlayStreamProvider : IPlayStreamProvider
{
    public void Use(IPlayStreamProviderListener env)
    {
        MainWindow mw = new(env);
        mw.Show();
    }

    public string Name => "Local Library";
    public string Description => "Allows you to sort and play local files";
    public WrappedStream Icon => new WrappedEmbeddedResourceStream(typeof(LocalLibraryPlayStreamProvider).Assembly,
        "SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary.icon.svg");
    public Version? Version => typeof(LocalLibraryPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary\nGPL3.0";
    public List<Tuple<Uri, URLType>>? Links { get; }
}
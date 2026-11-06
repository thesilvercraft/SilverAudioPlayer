using System.Composition;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

[Export(typeof(IPlayStreamProvider))]
public sealed class SLibIndexPlayStreamProvider : IPlayStreamProvider
{
    private readonly HttpClient _httpClient = new();
    private SLibIndexWindow? _window;
    private string _serverUrl = "http://192.168.1.173:32542/api";

    public string Name => "SLibIndex Provider";
    public string Description => "Provides music from an SLibIndex server";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(SLibIndexPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "GPL-3.0-only";
    public List<Tuple<Uri, URLType>>? Links => null;

    public void Use(IPlayStreamProviderListener env)
    {
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        _window = new SLibIndexWindow(_httpClient, env, _serverUrl, url => _serverUrl = url);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }
}
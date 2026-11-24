using System.Composition;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

[Export(typeof(IPlayStreamProvider))]
[Export(typeof(IMusicStatusInterface))]
public sealed class SLibIndexPlayStreamProvider : IPlayStreamProvider, IMusicStatusInterface
{
    private static readonly SLibIndexAutoPlayController SharedController = new();
    private SLibIndexWindow? _window;

    public string Name => "SLibIndex Provider";
    public string Description => "Provides music from an SLibIndex server with persistent Auto-Play recommendations";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(SLibIndexPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "GPL-3.0-only";
    public List<Tuple<Uri, URLType>>? Links => null;

    public bool IsStarted => SharedController.IsStarted;

    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        SharedController.AttachStatusListener(listener);
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        SharedController.DetachStatusListener(listener);
    }

    public void Use(IPlayStreamProviderListener env)
    {
        SharedController.AttachPlayListener(env);

        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        _window = new SLibIndexWindow(SharedController);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void Dispose()
    {
    }
}

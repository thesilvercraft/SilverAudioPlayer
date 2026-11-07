using System.Diagnostics;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;

namespace SilverAudioPlayer.Core;

public class MusicStatusCoordinator
{
    private readonly IMediator _mediator;
    public readonly List<IMusicStatusInterface> LoadedMusicStatusInterfaces = [];
    private IMusicStatusInterfaceListenerAdmin? _musicStatusInterface;

    public MusicStatusCoordinator(IMediator mediator) =>
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    // Called by Logic's mediator subscription. Public methods below publish messages,
    // while these handlers perform the existing coordinator work.
    public void HandleMessage(IMessage message)
    {
        switch (message)
        {
            case TrackChangedMessage m: HandleTrackChanged(m.Song); break;
            case PlaybackStateChangedMessage m: HandlePlaybackStateChanged(m.State); break;
            case PlaybackPositionChangedMessage m: HandlePositionChanged(m.Position); break;
            case AddMusicStatusInterfaceMessage m: AddMsiCore(m.StatusInterface, m.Listener); break;
            case RemoveMusicStatusInterfaceMessage m: RemoveMsiCore(m.StatusInterface, m.Listener); break;
            case OpenMusicStatusInterfacesMessage m:
                OpenMusicStatusInterfacesCore(m.Listener, m.Interfaces, m.IsPluginAllowed);
                break;
        }
    }

    public void AddMSI(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
        => _mediator.SendCommand(new AddMusicStatusInterfaceMessage(statusInterface, logicMusicInterfaceListener));

    private void AddMsiCore(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
    {
        LoadedMusicStatusInterfaces.Add(statusInterface);
        _musicStatusInterface = logicMusicInterfaceListener;
        statusInterface.StartIPC(logicMusicInterfaceListener);
    }

    public void RemoveMsi(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListener logicMusicInterfaceListener)
        => _mediator.SendCommand(new RemoveMusicStatusInterfaceMessage(statusInterface, logicMusicInterfaceListener));

    private void RemoveMsiCore(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListener logicMusicInterfaceListener)
    {
        statusInterface.StopIPC(logicMusicInterfaceListener);
        statusInterface.Dispose();
        LoadedMusicStatusInterfaces.Remove(statusInterface);
    }

    public void TrackChangedNotification(Song? currentSong)
        => _mediator.SendCommand(new TrackChangedMessage(currentSong));

    private void HandleTrackChanged(Song? currentSong)
    {
        if (currentSong != null)
        {
            _musicStatusInterface?.FireTrackChangedNotification(currentSong);
        }
    }

    public void PlaybackStateChangedNotification(PlaybackState s, IEnumerable<IWakeLockProvider>? wakeLockInterfaces)
    {
        _mediator.SendCommand(new PlaybackStateChangedMessage(s));
        if (wakeLockInterfaces == null) return;
        var wakeLocks = wakeLockInterfaces.ToList();
        Task.Run(() =>
        {
            switch (s)
            {
                case PlaybackState.Playing: Parallel.ForEach(wakeLocks, provider => provider.WakeLock()); break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused: Parallel.ForEach(wakeLocks, provider => provider.UnWakeLock()); break;
            }
        });
    }

    private void HandlePlaybackStateChanged(PlaybackState s)
    {
        _musicStatusInterface?.FirePlayerStateChanged(s);
    }

    public void FirePositionChanged(TimeSpan pos)
        => _mediator.SendCommand(new PlaybackPositionChangedMessage(pos));

    private void HandlePositionChanged(TimeSpan pos)
    {
        _musicStatusInterface?.FirePositionChanged(pos);
    }

    public void MainWindow_Opened(IMusicStatusInterfaceListenerAdmin musicStatusInterface,
        IEnumerable<IMusicStatusInterface>? musicStatusInterfaces,
        Func<string, bool> checkIfPluginAllowed)
        => _mediator.SendCommand(new OpenMusicStatusInterfacesMessage(
            musicStatusInterface, musicStatusInterfaces, checkIfPluginAllowed));

    public void OpenMusicStatusInterfacesCore(IMusicStatusInterfaceListenerAdmin musicStatusInterface,
        IEnumerable<IMusicStatusInterface>? musicStatusInterfaces,
        Func<string, bool> checkIfPluginAllowed)
    {
        if (musicStatusInterfaces?.Any() != true) return;

        Task.Run(() =>
        {
            Parallel.ForEach(musicStatusInterfaces, dangthing =>
            {
                var fullName = dangthing.GetType().FullName ?? string.Empty;
                Debug.WriteLine("mainwindowload loading " + fullName);
                if (!checkIfPluginAllowed(fullName)) return;
                GC.KeepAlive(dangthing);
                AddMSI(dangthing, musicStatusInterface);
                Debug.WriteLine("mainwindowload loaded " + fullName);
            });
        });
    }
}
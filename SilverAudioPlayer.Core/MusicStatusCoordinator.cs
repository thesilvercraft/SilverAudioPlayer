using System.Diagnostics;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class MusicStatusCoordinator
{
    public readonly List<IMusicStatusInterface> LoadedMusicStatusInterfaces = [];
    private IMusicStatusInterfaceListenerAdmin? _musicStatusInterface;

    public void AddMSI(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
    {
        LoadedMusicStatusInterfaces.Add(statusInterface);
        _musicStatusInterface = logicMusicInterfaceListener;
        statusInterface.StartIPC(logicMusicInterfaceListener);
    }

    public void RemoveMsi(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListener logicMusicInterfaceListener)
    {
        statusInterface.StopIPC(logicMusicInterfaceListener);
        statusInterface.Dispose();
        LoadedMusicStatusInterfaces.Remove(statusInterface);
    }

    public void TrackChangedNotification(Song? currentSong)
    {
        if (currentSong != null)
        {
            _musicStatusInterface?.FireTrackChangedNotification(currentSong);
        }
    }

    public void PlaybackStateChangedNotification(PlaybackState s, IEnumerable<IWakeLockProvider>? wakeLockInterfaces)
    {
        _musicStatusInterface?.FirePlayerStateChanged(s);

        if (wakeLockInterfaces == null) return;
        var wakeLocks = wakeLockInterfaces.ToList();
        Task.Run(() =>
        {
            switch (s)
            {
                case PlaybackState.Playing:
                    Parallel.ForEach(wakeLocks, provider => provider.WakeLock());
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    Parallel.ForEach(wakeLocks, provider => provider.UnWakeLock());
                    break;
            }
        });
    }

    public void FirePositionChanged(TimeSpan pos)
    {
        _musicStatusInterface?.FirePositionChanged(pos);
    }

    public void MainWindow_Opened(IMusicStatusInterfaceListenerAdmin musicStatusInterface,
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

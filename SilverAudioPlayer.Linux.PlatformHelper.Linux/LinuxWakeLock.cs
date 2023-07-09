using System.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SilverAudioPlayer.Shared;
using Tmds.DBus;
using ScreenSaver.DBus;
namespace SilverAudioPlayer.Linux.PlatformHelper.Linux;

[Export(typeof(IWakeLockProvider))]
public class LinuxWakeLock : IWakeLockProvider
{
    public LinuxWakeLock()
    {
        var dSession = Connection.Session;
        _screenSaver = dSession.CreateProxy<IScreenSaver>("org.freedesktop.ScreenSaver",
            "/org/freedesktop/ScreenSaver");
    }

    private IScreenSaver _screenSaver;
    public string Name => "Linux wakelock org.freedesktop.ScreenSaver";

    public string Description => "Uses Dbus";

    public WrappedStream Icon => null;

    public Version? Version => typeof(LinuxWakeLock).Assembly.GetName().Version;

    public string Licenses => "GPL3.0";

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Linux.PlatformHelper.Linux"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri("https://specifications.freedesktop.org/idle-inhibit-spec/latest/re01.html"),
            URLType.LibraryDocumentation),
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/tmds/Tmds.DBus"),
            URLType.LibraryCode)
    };

    private uint? Cookie = null;
    public void WakeLock()
    {
        Task.Run(async () =>
        {
            Cookie = await _screenSaver.InhibitAsync(
                "org.mpris.MediaPlayer2.sap.i" + Environment.ProcessId,
                "Playing music");
            Debug.WriteLine("Obtained cookie {0}", Cookie);
        });
    }

    public void UnWakeLock()
    {
        if (Cookie == null) return;
        Task.Run(async () =>
        {
            if (Cookie is not null)
            {
                Debug.WriteLine("Invalidate cookie {0}", Cookie);

                await _screenSaver.UnInhibitAsync((uint)Cookie);
            }
        });      
    }

  
}
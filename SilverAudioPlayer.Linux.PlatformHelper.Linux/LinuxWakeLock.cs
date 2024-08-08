using System.Composition;
using System.Diagnostics;
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

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(LinuxWakeLock).Assembly,
        "SilverAudioPlayer.Linux.PlatformHelper.Linux.LinuxPlatformHelper.svg");

    public Version? Version => typeof(LinuxWakeLock).Assembly.GetName().Version;

    public string Licenses => "SilverAudioPlayer.Linux.PlatformHelper.Linux.LinuxPlatformHelper is a part of the SilverAudioPlayer project and is licensed under the GPL3.0 license";

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Linux.PlatformHelper.Linux"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri("https://specifications.freedesktop.org/idle-inhibit-spec/latest/re01.html"),
            URLType.LibraryDocumentation),
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/tmds/Tmds.DBus"),
            URLType.LibraryCode)
    ];

    private uint? Cookie = null;
    public void WakeLock()
    {
        Task.Run(async () =>
        {
            Cookie = await _screenSaver.InhibitAsync(
                "Catenoid",
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
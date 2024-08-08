using SilverAudioPlayer.Shared;
using Tmds.DBus;
using System.Composition;
using System.Diagnostics;


namespace SilverAudioPlayer.Linux.MPRIS
{
    [Export(typeof(IMusicStatusInterface))]
    public class Mpris : IMusicStatusInterface
    {
        public bool IsStarted => _IsStarted;
        private bool _IsStarted;
        public Mpris()
        {
        }
        Connection connection;
        MprisPlayer player;
        void start()
        {
            if (_IsStarted)
            {
                return;
            }
            _IsStarted = true;
            Task.Run(async () =>
            {
                try
                {
                    connection = new Connection(Address.Session);
                    connection.StateChanged+=ConnectionOnStateChanged;
                    await connection.ConnectAsync();
                    player =new MprisPlayer(Env);
                    await connection.RegisterObjectAsync(player);
                    await connection.RegisterServiceAsync("org.mpris.MediaPlayer2.catenoid.i_" + Environment.ProcessId);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    throw;
                }
            });
        }

        private void ConnectionOnStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            if (e.State == ConnectionState.Disconnected && _IsStarted)
            {
               _IsStarted=false;
            }
            Debug.WriteLine(e.DisconnectReason);
        }

        public void Dispose()
        {
        }
        public ObjectPath ObjectPath { get; } = new ObjectPath("/org/mpris/MediaPlayer2");
        private IMusicStatusInterfaceListener Env;
        public void StartIPC(IMusicStatusInterfaceListener listener)
        {
            Env = listener;
            start();
        }

        public void StopIPC(IMusicStatusInterfaceListener listener)
        {
            _IsStarted = false;
            Debug.WriteLine("Mpris stopipc");
            connection.UnregisterObject(player);
            player.CleanDirUp();
            connection.Dispose();
        }

        public string Name => "MPRIS Linux MSI";
        public string Description => "Interface with linux dbus for native music controls";
        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(Mpris).Assembly,
            "SilverAudioPlayer.Linux.MPRIS.Mpris.svg");

        public Version? Version => typeof(Mpris).Assembly.GetName().Version;
        public string Licenses => "SilverAudioPlayer.Linux.MPRIS is a part of the SilverAudioPlayer project and is licensed under the GPL3.0 license";
        public List<Tuple<Uri, URLType>>? Links =>
        [
            new Tuple<Uri, URLType>(
                new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Linux.MPRIS"),
                URLType.Code),
            new Tuple<Uri, URLType>(
                new Uri("https://specifications.freedesktop.org/mpris-spec/latest/"),
                URLType.LibraryDocumentation),
            new Tuple<Uri, URLType>(
                new Uri("https://wiki.archlinux.org/title/MPRIS"),
                URLType.LibraryDocumentation),
            new Tuple<Uri, URLType>(
                new Uri("https://github.com/tmds/Tmds.DBus"),
                URLType.LibraryCode)
        ];





    }
}
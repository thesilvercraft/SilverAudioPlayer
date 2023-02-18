using SilverAudioPlayer.Shared;
using Tmds.DBus;
using System.Composition;
using System.Diagnostics;


namespace SilverAudioPlayer.Linux.MPRIS
{
    [Export(typeof(IMusicStatusInterface))]
    public class MPRIS :IMusicStatusInterface, IMediaPlayer2,IPlayer
    {
        public MPRIS()
        {
            Task.Run(async ()=>
            {
                try
                {
                    var connection = new Connection(Address.Session!);

                    await connection.ConnectAsync();

                    await connection.RegisterObjectAsync(this);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    throw;
                }
             
            });
        }

        public void Dispose()
        {
        }
        public ObjectPath ObjectPath { get; } = new ObjectPath("/org/mpris/MediaPlayer2");

      
        public void StartIPC()
        {
            

        }

        public void StopIPC()
        {
        }
        public async Task RaiseAsync()
        {
        }

        public async Task QuitAsync()
        {
        }
        public void TrackChangedNotification(Song newtrack)
        {
        }

        public void PlayerStateChanged(PlaybackState newstate)
        {
        }

        public Task NextAsync()
        {
            throw new NotImplementedException();
        }

        public Task PreviousAsync()
        {
            throw new NotImplementedException();
        }

        public Task PauseAsync()
        {
            Pause?.Invoke(this,EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task PlayPauseAsync()
        {
            PlayPause?.Invoke(this,EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stop?.Invoke(this,EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task PlayAsync()
        {
            Play?.Invoke(this,EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SeekAsync(long Offset)
        {
            throw new NotImplementedException();
        }

        public Task SetPositionAsync(ObjectPath TrackId, long Position)
        {
            throw new NotImplementedException();
        }

        public Task OpenUriAsync(string Uri)
        {
            //TODO
            return Task.CompletedTask;
        }

        private Action<long> OnSeeked;
        public async Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
        {
            OnSeeked = handler;
            return this;
        }


        public Task<object> GetAsync(string prop)
        {
            Debug.WriteLine(prop);
            return null;
        }

        Task<PlayerProperties> IPlayer.GetAllAsync()
        {
            return Task.FromResult(new PlayerProperties()
            {
PlaybackStatus = GetState().ToString(),
Rate = 1,
MaximumRate = 1,
MinimumRate = 1,
CanControl = true,
CanPause = true,
CanPlay = true,
CanGoNext = true,
CanGoPrevious = true,
CanSeek = true,
            });
        }

        public Task<MediaPlayer2Properties> GetAllAsync()
        {
            return Task.FromResult(new MediaPlayer2Properties()
            {
                HasTrackList = true
            });
        }

        public Task SetAsync(string prop, object val)
        {
        }

        public void StartIPC(IMusicStatusInterfaceListener listener)
        {
        }

        public void StopIPC(IMusicStatusInterfaceListener listener)
        {
            throw new NotImplementedException();
        }

        public string Name => "MPRIS Linux MSI";
        public string Description => "Interface with linux dbus for native music controls";
        public WrappedStream? Icon => null;
        public Version? Version => typeof(MPRIS).Assembly.GetName().Version;
        public string Licenses => "GPL3.0";
        public List<Tuple<Uri, URLType>>? Links => new()
        {
            new Tuple<Uri, URLType>(
                new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Linux.MPRIS"),
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
        };
        

       

     
    }
}
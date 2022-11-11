using SilverAudioPlayer.Shared;
using Tmds.DBus;
using System.Composition;


namespace SilverAudioPlayer.Linux.MPRIS
{
    [Export(typeof(IMusicStatusInterface))]
    public class MPRIS :IMusicStatusInterface, IMediaPlayer2,IPlayer
    {
        public MPRIS()
        {
            Task.Run(async ()=>
            {
                await Connection.System.RegisterObjectAsync(this);
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
            throw new NotImplementedException();
        }

        public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
        {
            throw new NotImplementedException();
        }

        public Task<T> GetAsync<T>(string prop)
        {
            throw new NotImplementedException();
        }

        Task<PlayerProperties> IPlayer.GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<MediaPlayer2Properties> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetAsync(string prop, object val)
        {
            throw new NotImplementedException();
        }

        public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
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
        

        public event EventHandler? Play;
        public event EventHandler? Pause;
        public event EventHandler? PlayPause;
        public event EventHandler? Stop;
        public event EventHandler? Next;
        public event EventHandler? Previous;
        public event EventHandler<byte>? SetVolume;
        public event Func<byte>? GetVolume;
        public event Func<Song>? GetCurrentTrack;
        public event Func<ulong>? GetDuration;
        public event EventHandler<ulong>? SetPosition;
        public event Func<ulong>? GetPosition;
      

        public event Func<PlaybackState>? GetState;
        public event EventHandler<IMusicStatusInterface>? StateChangedNotification;
        public event EventHandler<IMusicStatusInterface>? RepeatChangedNotification;
        public event Func<RepeatState>? GetRepeat;
        public event EventHandler<RepeatState>? SetRepeat;
        public event EventHandler<IMusicStatusInterface>? ShutdownNotiifcation;
        public event EventHandler<IMusicStatusInterface>? ShuffleChangedNotification;
        public event Func<bool>? GetShuffle;
        public event EventHandler<bool>? SetShuffle;
        public event EventHandler<IMusicStatusInterface>? RatingChangedNotification;
        public event EventHandler<byte>? SetRating;
        public event EventHandler<IMusicStatusInterface>? CurrentTrackNotification;
        public event EventHandler<IMusicStatusInterface>? CurrentLyricsNotification;
        public event EventHandler<IMusicStatusInterface>? NewLyricsNotification;
        public event EventHandler<IMusicStatusInterface>? NewCoverNotification;
        public event Func<string>? GetLyrics;

     
    }
}
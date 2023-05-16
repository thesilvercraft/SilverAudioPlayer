using SilverAudioPlayer.Shared;
using Tmds.DBus;
using System.Composition;
using System.Diagnostics;


namespace SilverAudioPlayer.Linux.MPRIS
{
    [Export(typeof(IMusicStatusInterface))]
    public class Mpris :IMusicStatusInterface, IMediaPlayer2,IPlayer
    {
        public event Action<PropertyChanges> OnPropertiesChanged;

        public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
        {
            Debug.WriteLine("Someone cares?");
            return SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);
        }
        public Mpris()
        {
            Task.Run(async ()=>
            {
                try
                {
                    var connection = new Connection(Address.Session!);
                    await connection.ConnectAsync();
                    await connection.RegisterServiceAsync("org.mpris.MediaPlayer2.silveraudioplayer.instance"+Environment.ProcessId);
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

      
       
        public async Task RaiseAsync()
        {
        }

        public async Task QuitAsync()
        {
        }

    
        public Task NextAsync()
        {
            Env.Next();
            return Task.CompletedTask;
        }

        public Task PreviousAsync()
        {
            Env.Previous();
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            Env.Pause();
            return Task.CompletedTask;
        }

        public Task PlayPauseAsync()
        {
            Env.PlayPause();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Env.Stop();
            return Task.CompletedTask;
        }

        public Task PlayAsync()
        {
            Env.Play();
            return Task.CompletedTask;
        }

        public Task SeekAsync(long Offset)
        {
            Debug.WriteLine("[MPRIS] Failing to SeekAsync");

            throw new NotImplementedException();
        }

        public Task SetPositionAsync(ObjectPath TrackId, long Position)
        {
            Debug.WriteLine("[MPRIS] Failing to SetPositionAsync");

            throw new NotImplementedException();
        }

        public Task OpenUriAsync(string Uri)
        {
            if (Env is IPlayStreamProviderListener l)
            {
                l.ProcessFiles(new []{Uri});
            }
            return Task.CompletedTask;
        }

        private Action<long> OnSeeked;
        public async Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
        {
            OnSeeked = handler;
            return this;
        }

        string InternalStatus()
        {
            return Env?.GetState() switch
            {
                PlaybackState.Playing=>"Playing",
                PlaybackState.Paused => "Paused",
                _ => "Stopped"
            };
        }


        public async Task<object> GetAsync(string prop)
        {
            switch (prop.ToLowerInvariant())
            {
                case "playbackstatus":
                return InternalStatus();
                case "canseek": 
                case "canpause": 
                case "canplay": 
                case "cangonext": 
                case "cangoprevious": 
                case "cancontrol": 
                    return true;
                case "minimumrate": 
                case "maximumrate": 
                case "rate": 
                    return 1;
                //Metadata
                case "position":
                    return (long)Env.GetPositionMilli()*1000;
                case "volume":
                    return Env.GetVolume() / 255;
                case "loopstatus":
                    return GetLoopStatus();
                case "identity":
                    return "SilverAudioPlayer";
                case "shuffle":
                    return false;
                case "metadata":
                    return GetMetadata();
            }
            Debug.WriteLine("[MPRIS] Failing to answer for "+prop);

            return null;
        }

        Dictionary<string, object> GetMetadata()
        {
            var track = Env?.GetCurrentTrack();
            return new Dictionary<string, object>()
            {
                {"mpris:length",( (long?)Env?.GetDurationMilli()??10)*1000},
                {"mpris:trackid", new ObjectPath("/org/silvercraft/t"+track?.Guid.ToString("N"))},
                {"mpris:artUrl", "http://localhost:36169/albumart"}, 
                {"xesam:title", track?.Metadata?.Title ?? "nothing"},
                {"xesam:album", track?.Metadata?.Album ?? "no"},
                {"xesam:artist", new string[] {track?.Metadata?.Artist??"none"}}
            };
        }
        string GetLoopStatus()
        {
            //   LoopStatus None Track Playlist 
            return  "None";
        }
        Task<PlayerProperties> IPlayer.GetAllAsync()
        {
            return Task.FromResult(new PlayerProperties()
            {
PlaybackStatus =InternalStatus(),
Rate = 1,
MaximumRate = 1,
MinimumRate = 1,
CanControl = true,
CanPause = true,
CanPlay = true,
CanGoNext = true,
CanGoPrevious = true,
CanSeek = true,
Metadata = GetMetadata(),

            });
        }

        public Task<MediaPlayer2Properties> GetAllAsync()
        {
            return Task.FromResult(new MediaPlayer2Properties()
            {
                HasTrackList = false,
                CanQuit = false,
                CanRaise = false,
                DesktopEntry = "silveraudioplayer",
                Identity = "SilverAudioPlayer",
                SupportedUriSchemes = new []{"http","https"},
                SupportedMimeTypes = new []{"audio/ogg","audio/flac"},
            });
        }

        public Task SetAsync(string prop, object val)
        {
            Debug.WriteLine("[MPRIS] NOT SETTING "+prop);
            return Task.CompletedTask;
        }

        private IMusicStatusInterfaceListener Env;
        public void StartIPC(IMusicStatusInterfaceListener listener)
        {
            Env = listener;
            Env.PlayerStateChanged += PlayerStateChanged;
            Env.TrackChangedNotification += TrackChanged;
        }
        private void TrackChanged(object? sender, Song e)
        {
            OnPropertiesChanged?.Invoke(new PropertyChanges(new List<KeyValuePair<string,object>>()
            {
                new("Metadata", GetMetadata()),
            }.ToArray()));
        }

        private void PlayerStateChanged(object? sender, PlaybackState e)
        {
            OnPropertiesChanged?.Invoke(new PropertyChanges(new List<KeyValuePair<string,object>>()
            {
                new("PlaybackStatus", Env?.GetState() switch
                {
                    PlaybackState.Playing=>"Playing",
                    PlaybackState.Paused => "Paused",
                    _ => "Stopped"
                }),
            }.ToArray()));
        }

        public void StopIPC(IMusicStatusInterfaceListener listener)
        {
        }

        public string Name => "MPRIS Linux MSI";
        public string Description => "Interface with linux dbus for native music controls";
        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(Mpris).Assembly,
            "SilverAudioPlayer.Linux.MPRIS.Mpris.svg");

        public Version? Version => typeof(Mpris).Assembly.GetName().Version;
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
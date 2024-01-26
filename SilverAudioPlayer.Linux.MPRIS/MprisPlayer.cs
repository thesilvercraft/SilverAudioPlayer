
// Create a class that inherits from the interface
using Tmds.DBus;
using DBusInterop;
using System.Diagnostics;
using SilverAudioPlayer.Shared;
namespace SilverAudioPlayer.Linux.MPRIS;
public class MprisPlayer : IPlayer, IMediaPlayer2
{
    private readonly IMusicStatusInterfaceListener Env;

    public MprisPlayer(IMusicStatusInterfaceListener listner)
    {
        Env = listner;
        Env.TrackChangedNotification += OnNewTrack;
        Env.PlayerStateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, PlaybackState e)
    {
        Task.Run(async () =>
        {
            var propertyChanges = new PropertyChanges(new KeyValuePair<string, object>[1]
        {
            new KeyValuePair<string, object>("PlaybackStatus", await GetPlaybackStatusAsync())
        });

            PropertyChanged.Invoke(propertyChanges);

        });
    }


    private void OnNewTrack(object? sender, Song e)
    {
        Task.Run(async () =>
        {
            var propertyChanges = new PropertyChanges(new KeyValuePair<string, object>[1]
        {
            new KeyValuePair<string, object>("Metadata", await GetMetadataAsync())
        });

            PropertyChanged.Invoke(propertyChanges);

        });

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
        Env.SetPosition(Env.GetPosition() + (Offset / 1000000d));
        return Task.CompletedTask;
    }

    public Task SetPositionAsync(ObjectPath TrackId, long Position)
    {
        Env.SetPosition(Position / 1000000);
        return Task.CompletedTask;
    }

    public Task OpenUriAsync(string Uri)
    {
        if (Env is IPlayStreamProviderListener l)
        {
            l.ProcessFiles(new[] { Uri });
        }
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> GetMetadataAsync()
    {
        // Return some mock metadata
        var track = Env?.GetCurrentTrack();
        return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
            {
                {"mpris:length",( (long?)Env?.GetDuration()??10)*1000*1000},
                {"mpris:trackid", new ObjectPath("/org/mpris/MediaPlayer2/Track/"+"0")},
                {"mpris:artUrl", "http://localhost:36169/albumart"},
                {"xesam:title", track?.Metadata?.Title ?? "nothing"},
                {"xesam:album", track?.Metadata?.Album ?? "no"},
                {"xesam:artist", new string[] {track?.Metadata?.Artist??"none"}}
            });

    }

    public Task<string> GetPlaybackStatusAsync()
    {
        // Return some mock status
        //"Playing", "Paused" or "Stopped". 
        return Task.FromResult(Env?.GetState() switch
        {
            PlaybackState.Playing => "Playing",
            PlaybackState.Paused => "Paused",
            _ => "Stopped"
        });
    }

    public Task<string> GetLoopStatusAsync()
    {
        // Return some mock status
        return Task.FromResult("None");
    }

    public Task SetLoopStatusAsync(string value)
    {
        Console.WriteLine($"Set loop status {value}");
        return Task.CompletedTask;
    }

    public Task<double> GetRateAsync()
    {
        // Return some mock rate
        return Task.FromResult(1.0);
    }

    public Task SetRateAsync(double value)
    {
        Console.WriteLine($"Set rate {value}");
        return Task.CompletedTask;
    }

    public Task<bool> GetShuffleAsync()
    {
        // Return some mock shuffle
        return Task.FromResult(false);
    }

    public Task SetShuffleAsync(bool value)
    {
        Console.WriteLine($"Set shuffle {value}");
        return Task.CompletedTask;
    }

    public Task<long> GetVolumeAsync()
    {
        // Return some mock volume
        return Task.FromResult(100L);
    }

    public Task SetVolumeAsync(long value)
    {
        Console.WriteLine($"Set volume {value}");
        return Task.CompletedTask;
    }

    public Task<long> GetPositionAsync()
    {
        return Task.FromResult((long)(Env.GetPosition() * 1000 * 1000));
    }

    public Task<double> GetMinimumRateAsync()
    {
        return Task.FromResult(1.0);
    }

    public Task<double> GetMaximumRateAsync()
    {
        return Task.FromResult(1.0);
    }

    public Task<bool> GetCanGoNextAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetCanGoPreviousAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetCanPlayAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetCanPauseAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetCanSeekAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetCanControlAsync()
    {
        // Return some mock capability
        return Task.FromResult(true);
    }

    public ObjectPath ObjectPath => new ObjectPath("/org/mpris/MediaPlayer2");

    public event Action<long> Seeked;
    public event Action<PropertyChanges> PropertyChanged;
    public event Action<PropertyChanges> BoringPropertyChanged;



    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(Seeked), handler));
    }

    public async Task<object> GetAsync(string prop)
    {
        Debug.WriteLine(prop);
        prop = prop.ToLower();
        switch (prop)
        {
            case "cancontrol":
            case "cangonext":
            case "cangoprevious":
            case "canpause":
            case "canseek":
                return Task.FromResult<object>(true);
            case "metadata":
                return await GetMetadataAsync();
            case "playbackstatus":
                return Task.FromResult<object>(await GetPlaybackStatusAsync());
            case "position":
                return Task.FromResult<object>(await GetPositionAsync());
            case "volume":
                return Task.FromResult<object>(await GetVolumeAsync());
        }
        throw new NotImplementedException();
    }

    public async Task<PlayerProperties> GetAllAsync()
    {
        return new PlayerProperties()
        {
            PlaybackStatus = await GetPlaybackStatusAsync(),
            CanControl = true,
            CanGoNext = true,
            CanGoPrevious = true,
            CanPause = true,
            CanPlay = true,
            CanSeek = true,
            Metadata = await GetMetadataAsync(),
            Position = await GetPositionAsync(),
            Volume = await GetVolumeAsync(),
        };
    }


    public Task SetAsync(string prop, object val)
    {
        throw new NotImplementedException();
    }

    Task<IDisposable> IPlayer.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(PropertyChanged), handler));

    }

    Task<IDisposable> IMediaPlayer2.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(BoringPropertyChanged), handler));

    }
    public Task QuitAsync()
    {
        return Task.CompletedTask;
    }

    public Task RaiseAsync()
    {
        return Task.CompletedTask;

    }

    async Task<MediaPlayer2Properties> IMediaPlayer2.GetAllAsync()
    {
        return new MediaPlayer2Properties()
        {
            CanQuit = true,
            CanRaise = true,
            DesktopEntry = "catenoid.desktop",
            Identity = "Catenoid",
            SupportedUriSchemes = ["http", "https"],
            SupportedMimeTypes = ["audio/mod"],

        };
    }
}

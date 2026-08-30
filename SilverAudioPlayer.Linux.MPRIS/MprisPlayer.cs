using Tmds.DBus;
using DBusInterop;
using System.Diagnostics;
using SilverAudioPlayer.Shared;
namespace SilverAudioPlayer.Linux.MPRIS;
public class MprisPlayer : IPlayer, IMediaPlayer2, IMprisTrackList
{
    private readonly IMusicStatusInterfaceListener Env;

    public MprisPlayer(IMusicStatusInterfaceListener listner)
    {
        Env = listner;
        Env.TrackChangedNotification += OnNewTrack;
        Env.PlayerStateChanged += OnStateChanged;
        Env.PositionChanged+= EnvOnPositionChanged;
        Directory.CreateDirectory(TempFolder);
    }

    private void EnvOnPositionChanged(object? sender, TimeSpan e)
    {
        Task.Run(() =>
        {
        Seeked.Invoke((long)(e.TotalSeconds*1000*1000));
        });
    }

    public void CleanDirUp()
    {
        Directory.Delete(TempFolder,true);
        Env.TrackChangedNotification -= OnNewTrack;
        Env.PlayerStateChanged -= OnStateChanged;
        Env.PositionChanged-= EnvOnPositionChanged;
    }
    private void OnStateChanged(object? sender, PlaybackState e)
    {
        Task.Run(async () =>
        {
            var propertyChanges = new PropertyChanges(new KeyValuePair<string, object>[1]
            {
                new("PlaybackStatus", await GetPlaybackStatusAsync())
            });
            PropertyChanged.Invoke(propertyChanges);
        });
    }
    static string TempFolder = Path.Combine(Path.GetTempPath(), $"sap-albumart-p{Environment.ProcessId}");

    string TempFile = Path.Combine(TempFolder,"1");
    bool HasAlbumArtWritten = false;
    private void OnNewTrack(object? sender, Song e)
    {
        Task.Run(async () =>
        {
            var x = Env.GetBestRepresentation(e?.Metadata?.Pictures);

            if (x != null)
            {
                TempFile= Path.Combine(TempFolder,x.Hash??e.Guid.ToString());
                if (x.Data.MimeType.FileExtensions is { Length: > 0 })
                {
                    TempFile+= x.Data.MimeType.FileExtensions[0];
                }
                await using var fs = new FileStream(TempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                await using var data = x.Data.GetStream();
                await data.CopyToAsync(fs);
                HasAlbumArtWritten = true;
            }
            else
            {
                HasAlbumArtWritten = false;
                if (File.Exists(TempFile))
                {
                    File.Delete(TempFile);
                }
            }

            var propertyChanges = new PropertyChanges(
            [
                new("Metadata", await GetMetadataAsync())
            ]);
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
        Env.SetPosition(Position / 1000000.0);
        return Task.CompletedTask;
    }

    public Task OpenUriAsync(string Uri)
    {
        if (Env is IPlayStreamProviderListener l)
        {
            l.ProcessFiles([Uri]);
        }
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> GetMetadataAsync()
    {
        //https://www.freedesktop.org/wiki/Specifications/mpris-spec/metadata/
        var track = Env?.GetCurrentTrack();
        var g = track?.Guid.ToString("N");
        var res=new Dictionary<string, object>()
            {
                {"mpris:length",((long?)Env?.GetDuration()??10)*1000*1000},
                {"mpris:trackid", new ObjectPath("/org/mpris/MediaPlayer2/Track/"+( string.IsNullOrWhiteSpace(g)? "NoTrack": g))},
                {"xesam:title", track?.Metadata?.Title ?? "nothing"},
                {"xesam:album", track?.Metadata?.Album ?? "no"},
                {"xesam:artist", new string[] {track?.Metadata?.Artist??"none"}},
                {"xesam:discNumber", track?.Metadata?.DiscNumber??0},
                {"xesam:trackNumber", track?.Metadata?.TrackNumber??0},
                {"xesam:genre", track?.Metadata?.Genre??""},
                {"xesam:asText", track?.Metadata?.Lyrics??""},
            };
            if(HasAlbumArtWritten)
            {
                var uri = new Uri("file://" + TempFile); // KDE seems to care about the file:// scheme in the status bar but not the lockscreen
                res["mpris:artUrl"]= uri.ToString();
            }
        return Task.FromResult<IDictionary<string, object>>(res);

    }

    public Task<string> GetPlaybackStatusAsync()
    {
        return Task.FromResult(Env?.GetState() switch
        {
            PlaybackState.Playing => "Playing",
            PlaybackState.Paused => "Paused",
            _ => "Stopped"
        });
    }

    public Task<string> GetLoopStatusAsync()
    {
        return Task.FromResult(Env.GetRepeat() switch
        {
            RepeatState.None => "None",
            RepeatState.One => "Track",
            RepeatState.Queue => "Playlist",
            _ => throw new NotImplementedException()
        });
    }

    public Task SetLoopStatusAsync(string value)
    {
        Env.SetRepeat(value switch
        {
            "None" => RepeatState.None,
            "Track" => RepeatState.One,
            "Playlist" => RepeatState.Queue,
            _ => throw new NotImplementedException()
        });
        return Task.CompletedTask;
    }

    public Task<double> GetRateAsync() => Task.FromResult(1.0);

    public Task SetRateAsync(double value)
    {
        Debug.WriteLine($"Set rate {value}");
        return Task.CompletedTask;
    }

    public Task<bool> GetShuffleAsync() => Task.FromResult(false);

    public Task SetShuffleAsync(bool value)
    {
        Debug.WriteLine($"Set shuffle {value}");
        return Task.CompletedTask;
    }

    public Task<double> GetVolumeAsync() => Task.FromResult(Env.GetVolume() / 100d);

    public Task SetVolumeAsync(double value)
    {
        Debug.WriteLine($"Set volume {value}");
        if (value is <= 1 and >= 0)
        {
            Env.SetVolume((byte)(value * 100d));
        }
        return Task.CompletedTask;
    }

    public Task<long> GetPositionAsync() => Task.FromResult((long)(Env.GetPosition() * 1000 * 1000));

    public Task<double> GetMinimumRateAsync() => Task.FromResult(1.0);

    public Task<double> GetMaximumRateAsync() => Task.FromResult(1.0);

    public Task<bool> GetCanGoNextAsync() => Task.FromResult(true);

    public Task<bool> GetCanGoPreviousAsync() => Task.FromResult(true);

    public Task<bool> GetCanPlayAsync() => Task.FromResult(true);

    public Task<bool> GetCanPauseAsync() => Task.FromResult(true);

    public Task<bool> GetCanSeekAsync() => Task.FromResult(true);

    public Task<bool> GetCanControlAsync() => Task.FromResult(true);

    public ObjectPath ObjectPath => new ObjectPath("/org/mpris/MediaPlayer2");

    public event Action<long> Seeked;
    public event Action<PropertyChanges> PropertyChanged;
    public event Action<PropertyChanges> BoringPropertyChanged;

    public event Action<(ObjectPath[] tracks, ObjectPath currentTrack)> TrackListReplaced;
    public event Action<(IDictionary<string, object> metadata, ObjectPath afterTrack)> TrackAdded;
    public event Action<ObjectPath> TrackRemoved;
    public event Action<(ObjectPath trackId, IDictionary<string, object> metadata)> TrackMetadataChanged;


    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(Seeked), handler));
    }

    public async Task<object> GetAsync(string prop)
    {
        prop = prop.ToLowerInvariant();
        return prop switch
        {
            "cancontrol" or "cangonext" or "cangoprevious" or "canpause" or "canseek" or "canplay" => true,
            "canraise" or "canquit" => false,
            "desktopentry" => DesktopEntry,
            "minimumrate" or "maximumrate" or "rate" => 1d,
            "metadata" => await GetMetadataAsync(),
            "playbackstatus" => await GetPlaybackStatusAsync(),
            "position" => await GetPositionAsync(),
            "volume" => await GetVolumeAsync(),
            "identity" => Identity,
            "shuffle" => await GetShuffleAsync(),
            "loopstatus" => GetLoopStatusAsync(),
            "supportedmimetypes" => await Mimes(),
            "supportedurischemes" => supportedUri,
            "tracks" => (await ((IMprisTrackList)this).GetAllAsync()).Tracks,
            "canedittracks" => false, 
            _ => throw new NotImplementedException(),
        };
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
        if (prop.ToLowerInvariant() == "volume")
        {
            SetVolumeAsync((double)val);
        }
        return Task.CompletedTask;

    }
    public event Action<PropertyChanges> TrackListPropertyChanged;

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(TrackListPropertyChanged), handler));
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
    async Task<string[]> Mimes()
    {
        var tm = await Env.SupportedMimeTypes();
        List<string> mimes = [.. tm.Select(x => x.Common), .. tm.SelectMany(x => x.AlternativeTypes)];
        return [.. mimes];
    }

    private string[] supportedUri = ["file", "http", "https"];
    const string Identity = "Catenoid";
    const string DesktopEntry = "Catenoid";
    async Task<MediaPlayer2Properties> IMediaPlayer2.GetAllAsync()
    {
        return new MediaPlayer2Properties()
        {
            CanQuit = false,
            CanRaise = false,
            DesktopEntry = DesktopEntry,
            Identity = Identity,
            SupportedUriSchemes = supportedUri,
            SupportedMimeTypes = await Mimes(),
        };
    }
    #region ITrackList Implementation

    public async Task<IDictionary<string, object>[]> GetTracksMetadataAsync(ObjectPath[] TrackIds)
    {
        var resultList = new List<IDictionary<string, object>>();
        var playlistQueue = await Env?.GetQueue() ?? [];

        foreach (var trackId in TrackIds)
        {
            var trackIdStr = trackId.ToString().ToLowerInvariant();

            var matchingSong = playlistQueue.FirstOrDefault(song => 
                trackIdStr.EndsWith(song.Guid.ToString("N").ToLowerInvariant()));

            if (matchingSong != null)
            {
                resultList.Add(BuildMetadataMapForSong(matchingSong));
            }
            else
            {
                resultList.Add(new Dictionary<string, object>());
            }
        }

        return resultList.ToArray();
    }

    public Task AddTrackAsync(string Uri, ObjectPath AfterTrack, bool SetAsCurrent)
    {
        if (Env is IPlayStreamProviderListener l)
        {
            l.ProcessFiles(new[] { Uri });
        }
        return Task.CompletedTask;
    }

    public Task RemoveTrackAsync(ObjectPath TrackId)
    {
        Debug.WriteLine($"MPRIS TrackList requested removal of: {TrackId}");
        return Task.CompletedTask;
    }

    public Task GoToAsync(ObjectPath TrackId)
    {
        return SetPositionAsync(TrackId, 0);
    }

    public Task<IDisposable> WatchTrackListReplacedAsync(Action<(ObjectPath[] tracks, ObjectPath currentTrack)> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(TrackListReplaced), handler));
    }

    public Task<IDisposable> WatchTrackAddedAsync(Action<(IDictionary<string, object> metadata, ObjectPath afterTrack)> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(TrackAdded), handler));
    }

    public Task<IDisposable> WatchTrackRemovedAsync(Action<ObjectPath> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(TrackRemoved), handler));
    }

    public Task<IDisposable> WatchTrackMetadataChangedAsync(Action<(ObjectPath trackId, IDictionary<string, object> metadata)> handler, Action<Exception> onError = null)
    {
        return Task.FromResult<IDisposable>(SignalWatcher.AddAsync(this, nameof(TrackMetadataChanged), handler));
    }
    
    private IDictionary<string, object> BuildMetadataMapForSong(Song track)
    {
        if (track == null) return new Dictionary<string, object>();

        var g = track.Guid.ToString("N").ToLowerInvariant();
        var res = new Dictionary<string, object>()
        {
            { "mpris:length", ((long?)Env?.GetDuration() ?? 10) * 1000 * 1000 }, 
            { "mpris:trackid", new ObjectPath("/org/mpris/MediaPlayer2/Track/" + (string.IsNullOrWhiteSpace(g) ? "NoTrack" : g)) },
            { "xesam:title", track.Metadata?.Title ?? "Unknown Title" },
            { "xesam:album", track.Metadata?.Album ?? "Unknown Album" },
            { "xesam:artist", new string[] { track.Metadata?.Artist ?? "Unknown Artist" } },
            { "xesam:discNumber", track.Metadata?.DiscNumber ?? 0 },
            { "xesam:trackNumber", track.Metadata?.TrackNumber ?? 0 },
            { "xesam:genre", track.Metadata?.Genre ?? "" },
            { "xesam:asText", track.Metadata?.Lyrics ?? "" }
        };
        var currentTrack = Env?.GetCurrentTrack();
        if (currentTrack != null && currentTrack.Guid == track.Guid && HasAlbumArtWritten)
        {
            res["mpris:artUrl"] = new Uri("file://" + TempFile).ToString();
        }

        return res;
    }
    async Task<TrackListProperties> IMprisTrackList.GetAllAsync()
    {
        IEnumerable<Song> playlistQueue =(await Env?.GetQueue()) ?? [];

        var trackPaths = playlistQueue.Select(song => 
        {
            var g = song?.Guid.ToString("N");
            return new ObjectPath("/org/mpris/MediaPlayer2/Track/" + (string.IsNullOrWhiteSpace(g) ? "NoTrack" : g));
        }).ToArray();

        if (trackPaths.Length == 0)
        {
            var currentMetadata = await GetMetadataAsync();
            if (currentMetadata.TryGetValue("mpris:trackid", out var idObj) && idObj is ObjectPath path)
            {
                trackPaths = new[] { path };
            }
        }

        return new TrackListProperties
        {
            Tracks = trackPaths,
            CanEditTracks = false 
        };
    }

    #endregion
}

using DiscordRPC;
using DiscordRPC.Logging;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace SilverAudioPlayer.DiscordRP;

[Export(typeof(IMusicStatusInterface))]
public class DiscordPlayTracker : IMusicStatusInterface
{
    public event EventHandler Play;

    public event EventHandler Pause;

    public event EventHandler PlayPause;

    public event EventHandler Stop;

    public event EventHandler Next;

    public event EventHandler Previous;

    public event EventHandler<byte> SetVolume;

    public event Func<byte> GetVolume;

    public event Func<Song> GetCurrentTrack;

    public event Func<ulong> GetDuration;

    public event EventHandler<ulong> SetPosition;

    public event Func<ulong> GetPosition;

    public event Func<PlaybackState> GetState;

    public event EventHandler<IMusicStatusInterface> StateChangedNotification;

    public event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

    public event Func<RepeatState> GetRepeat;

    public event EventHandler<RepeatState> SetRepeat;

    public event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

    public event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

    public event Func<bool> GetShuffle;

    public event EventHandler<bool> SetShuffle;

    public event EventHandler<IMusicStatusInterface> RatingChangedNotification;

    public event EventHandler<byte> SetRating;

    public event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

    public event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

    public event EventHandler<IMusicStatusInterface> NewLyricsNotification;

    public event EventHandler<IMusicStatusInterface> NewCoverNotification;

    public event Func<string> GetLyrics;

    private const string AppName = "SilverAudioPlayer";

    public DiscordPlayTracker(string id, IRememberRichPresenceURLs? richPresenceURLgetter) : this(id)
    {
        richPresenceURLs = richPresenceURLgetter;
    }

    public DiscordPlayTracker() : this("926595775574712370", File.Exists(Path.Combine(AppContext.BaseDirectory, "uploadtoimgur")) ? new RememberRichPresenceURLsUsingImgurAndAJsonFile() { Uploadit = true } : null)
    {
    }

    public DiscordPlayTracker(string id)
    {
        client = new(id);
        client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
        client.OnError += (_, e) => Debug.WriteLine($"An error occurred with Discord RPC Client: {e.Code} {e.Message}");
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void PlayerStateChanged(PlaybackState newstate)
    {
        switch (newstate)
        {
            case PlaybackState.Stopped:
                SStop();
                break;

            case PlaybackState.Playing:
                if (GetCurrentTrack != null)
                {
                    var ct = GetCurrentTrack();
                    SPlay(ct.URI, ct);
                }
                break;

            case PlaybackState.Paused:
                if (GetCurrentTrack != null)
                {
                    var ct = GetCurrentTrack();
                    SPause(ct.URI, ct);
                }
                break;

            case PlaybackState.Buffering:
                break;

            default:
                break;
        }
    }

    public void StartIPC()
    {
        client.Initialize();
    }

    public void TrackChangedNotification(Song newtrack)
    {
        ChangeSong(newtrack.URI, newtrack);
    }

    public void StopIPC()
    {
        Debug.WriteLine($"disable called");
        client.Deinitialize();
        client.Dispose();
    }

    public DiscordRpcClient client;
    public IRememberRichPresenceURLs? richPresenceURLs;

    private readonly Dictionary<string, string> tracks = new()
    {
        { "Human After All - Daft Punk", "human-after-all-daft-punk" },
        { "Homework - Daft Punk", "homework-daft-punk" },
        { "Star Road - Kitsune²", "star-road-kitsune" },
        { "Dancin - Aaron Smith", "dancin-aaron-smith" },
        { "Jet Set Radio  - Hideki Naganuma", "jst-ost" },
        { "Jet Set Radio - Richard Jacques", "jst-ost" },
        { "Jet Set Radio - Toronto", "jst-ost" },
        { "Jet Set Radio Original Soundtrack - Hideki Naganuma", "jst-ost" },
        { "Jet Set Radio Original Soundtrack - Richard Jacques", "jst-ost" },
        { "Jet Set Radio Original Soundtrack - Toronto", "jst-ost" },
        { "Audio Crime - 3kliksphilip", "audio-crime-3klks" },
        { "DELTARUNE Chapter 2 (Original Game Soundtrack) - Toby Fox", "deltarunech2" },
        { "DELTARUNE Chapter 1 (Original Game Soundtrack) - Toby Fox", "deltarunech1" },
        { "Thriller - Michael Jackson", "thriller-mj" },
        { "Marsupial Madness - Adhesive Wombat", "mm-aw" },
        { "Jetpack Joyride (Original Game Soundtrack) - Halfbrick", "jjost-hlfbrk" },
        { "Everybody Jam! - Scatman John", "ej-sctman" },
        { "Scatman's World - Scatman John", "sctmnswrld-sctman" }
    };

    public void ChangeSong(string? loc, Song a)
    {
        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        Debug.WriteLine($"cs called {artistandalbum}");

        var bigimage = GetAlbumArt(loc, a);
        SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState), StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap", bigimage == null ? AppName : a.Metadata?.Album ?? AppName, "start", "currently playing");
    }

    private static string StatusOrNotToStatus(string message, string status)
    {
        if (message.Length == 0)
        {
            return "";
        }
        if (message.Length > 10)
        {
            return message;
        }
        return status + " " + message;
    }

    public string? GetAlbumArt(string? loc, Song a)
    {
        if (richPresenceURLs != null)
        {
            var url = richPresenceURLs.GetURL(a);
            if (!string.IsNullOrEmpty(url))
            { return url; }
        }
        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        return tracks.GetValueOrDefault(artistandalbum, null);
    }

    private const string PauseTextState = "Currently paused";
    private const string PauseTextSState = "Paused";
    private const string PauseStateRPSMLICN = "pause";

    private const string PlayTextSState = "Playing";
    private const string PlayTextState = "Currently playing";
    private const string PlayStateRPSMLICN = "start";

    private const string StoppedTextSState = "Stopped";
    private const string StoppedTextState = "Currently stopped";
    private const string StoppedStateRPSMLICN = "stop";

    public void SPause(string? loc, Song a)
    {
        Debug.WriteLine($"Pause called");

        if (a != null)
        {
            var bigimage = GetAlbumArt(loc, a);
            SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState), StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap", bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PauseStateRPSMLICN, PauseTextState);
        }
        else
        {
            SetStatus(Path.GetFileNameWithoutExtension(loc), PauseTextSState, "sap", AppName, PauseStateRPSMLICN, PauseTextState);
        }
    }

    public void SPlay(string? loc, Song a)
    {
        Debug.WriteLine($"play called");

        if (a != null)
        {
            var bigimage = GetAlbumArt(loc, a);
            SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PlayTextSState), StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap", bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PlayStateRPSMLICN, PlayTextState);
        }
        else
        {
            SetStatus(Path.GetFileNameWithoutExtension(loc), PlayTextSState, "sap", AppName, PlayStateRPSMLICN, PlayTextState);
        }
    }

    public void SStop()
    {
        Debug.WriteLine($"stop called");

        SetStatus(StoppedTextSState, "Not playing anything", "sap", AppName, StoppedStateRPSMLICN, StoppedTextState);
    }

    private void SetStatus(string details, string state, string largeimage, string largeimagetext, string smallimage, string smallimagetext)
    {
        try
        {
            client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = largeimage,
                    LargeImageText = largeimagetext,
                    SmallImageKey = smallimage,
                    SmallImageText = smallimagetext
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
}

public interface IRememberRichPresenceURLs
{
    string? GetURL(Song track);
}

public class RememberRichPresenceURLsUsingImgurAndAJsonFile : IRememberRichPresenceURLs
{
    public bool Uploadit;
    private HttpClient httpClient;

    public string? GetURL(Song track)
    {
        Debug.WriteLine("Geturl");
        GetCache();
        Debug.WriteLine("uplodit " + Uploadit);
        if (track != null)
        {
            var a = track.Metadata?.Pictures?.FirstOrDefault(x => x.Data.Length > 1000);
            if (a == null)
            {
                return null;
            }
            if (cached.Any(x => x.hsh == a.Hash))
            {
                return cached.First(x => x.hsh == a.Hash).url.Replace("https", "http");
            }
            else if (Uploadit)
            {
                Debug.WriteLine("uplodit");
                var res = Upload(a.Data);
                res = res.Replace("https", "http");
                if (res != null)
                {
                    cached = new List<MscArtFile>(cached) { new(a.Hash, res) }.ToArray();
                    SetCache();
                }
                return res;
            }
        }
        return null;
    }

    public virtual string? Upload(byte[] bits)
    {
        var client = new ApiClient("d169c9264561822", "ce1616d067cb1493bc1df67b53e03660c5c02cc2");
        Debug.WriteLine("uploading " + bits.Length);
        if (httpClient == null)
        {
            httpClient = new HttpClient();
        }
        var imageEndpoint = new ImageEndpoint(client, httpClient);

        var imageUpload = imageEndpoint.UploadImageAsync(new MemoryStream(bits));
        var res = imageUpload.GetAwaiter().GetResult();
        Debug.WriteLine(res.Link);
        return res.Link;
    }

    private MscArtFile[] cached;

    private void GetCache()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "musicart.json")))
        {
            cached = System.Text.Json.JsonSerializer.Deserialize<MscArtFile[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "musicart.json")));
        }
        if (cached == null)
        {
            cached = System.Array.Empty<MscArtFile>();
        }
    }

    private void SetCache()
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "musicart.json"), System.Text.Json.JsonSerializer.Serialize(cached));
    }
}

record class MscArtFile(string hsh, string url);
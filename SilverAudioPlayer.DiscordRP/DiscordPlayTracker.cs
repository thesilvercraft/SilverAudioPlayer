using DiscordRPC;
using DiscordRPC.Logging;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using SilverAudioPlayer.Shared;
using System.Composition;
using System.Diagnostics;

namespace SilverAudioPlayer.DiscordRP;
public class DebugLogger : ILogger
{
    //
    // Summary:
    //     The level of logging to apply to this logger.
    public LogLevel Level { get; set; }

    // Summary:
    //     Creates a new instance of a Console Logger.
    public DebugLogger()
    {
        Level = LogLevel.Info;
    }


    //
    // Summary:
    //     Informative log messages
    //
    // Parameters:
    //   message:
    //
    //   args:
    public void Trace(string message, params object[] args)
    {
        if (Level <= LogLevel.Trace)
        {
            Debug.WriteLine("TRACE: " + message, args);
        }
    }

    //
    // Summary:
    //     Informative log messages
    //
    // Parameters:
    //   message:
    //
    //   args:
    public void Info(string message, params object[] args)
    {
        if (Level <= LogLevel.Info)
        {
               Debug.WriteLine("INFO: " + message, args);
        }
    }

    //
    // Summary:
    //     Warning log messages
    //
    // Parameters:
    //   message:
    //
    //   args:
    public void Warning(string message, params object[] args)
    {
        if (Level <= LogLevel.Warning)
        {
            Debug.WriteLine("WARN: " + message, args);
        }
    }

    //
    // Summary:
    //     Error log messsages
    //
    // Parameters:
    //   message:
    //
    //   args:
    public void Error(string message, params object[] args)
    {
        if (Level <= LogLevel.Error)
        {


            Debug.WriteLine("ERR : " + message, args);
        }
    }
}

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
        client = new(id)
        {
            SkipIdenticalPresence = false
        };
        client.Logger = new DebugLogger() { Level = LogLevel.Warning };
        client.OnError += (_, e) => Debug.WriteLine($"An error occurred with Discord RPC Client: {e.Code} {e.Message}");
    }

    public void Dispose()
    {
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

    public string Name => "Discord RP";

    public string Description => "Discord rich presence music status interface";

    public WrappedStream? Icon => null;

    public Version? Version => typeof(DiscordPlayTracker).Assembly.GetName().Version;

    public string Licenses => @"DiscordRichPresence - https://www.nuget.org/packages/DiscordRichPresence
MIT License

Copyright (c) 2021 Lachee

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
SilverAudioPlayer.DiscordRP
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.";

    public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.DiscordRP"), URLType.Code),
            new(new($"https://www.nuget.org/packages/DiscordRichPresence/{typeof(DiscordRpcClient).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/Lachee/discord-rpc-csharp/issues"),URLType.LibraryCode),
        };

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

        var imageEndpoint = new ImageEndpoint(client, Shared.HttpClient.Client);

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
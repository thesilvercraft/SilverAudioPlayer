using System.Composition;
using System.Diagnostics;
using System.Text.Json;
using DiscordRPC;
using DiscordRPC.Logging;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;

namespace SilverAudioPlayer.DiscordRP;

public class DebugLogger : ILogger
{
    // Summary:
    //     Creates a new instance of a Console Logger.
    public DebugLogger()
    {
        Level = LogLevel.Info;
    }

    //
    // Summary:
    //     The level of logging to apply to this logger.
    public LogLevel Level { get; set; }


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
        if (Level <= LogLevel.Trace) Debug.WriteLine("TRACE: " + message + args);
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
        if (Level <= LogLevel.Info) Debug.WriteLine("INFO: " + message + args);
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
        if (Level <= LogLevel.Warning) Debug.WriteLine("WARN: " + message + args);
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
        if (Level <= LogLevel.Error) Debug.WriteLine("ERR : " + message + args);
    }
}

[Export(typeof(IMusicStatusInterface))]
public class DiscordPlayTracker : IMusicStatusInterface, IAmConfigurable
{
    private const string AppName = "SilverAudioPlayer";

    private const string PauseTextState = "Paused";
    private const string PauseTextSState = "Paused";
    private const string PauseStateRPSMLICN = "pause";

    private const string PlayTextSState = "Playing";
    private const string PlayTextState = "Playing";
    private const string PlayStateRPSMLICN = "start";

    private const string StoppedTextSState = "Stopped";
    private const string StoppedTextState = "Stopped";
    private const string StoppedStateRPSMLICN = "stop";

    private readonly Dictionary<string, string> tracks = new();

    public DiscordRpcClient client;

    private readonly List<IConfigurableElement> ConfigurableElements;
    public IRememberRichPresenceURLs? richPresenceURLs;


    public DiscordPlayTracker() : this("926595775574712370")
    {
        richPresenceURLs = new RememberRichPresenceURLsUsingImgurAndAJsonFile { Uploadit = true };
    }

    public DiscordPlayTracker(string id)
    {
        client = new DiscordRpcClient(id)
        {
            SkipIdenticalPresence = false,
            Logger = new DebugLogger { Level = LogLevel.Warning }
        };
        client.OnError += (_, e) => Debug.WriteLine($"An error occurred with Discord RPC Client: {e.Code} {e.Message}");
        ConfigurableElements = new List<IConfigurableElement>
        {
            new SimpleCheckBox
            {
                GetContent = () => "Allow imgur uploads", Checked = c =>
                {
                    if (c && !File.Exists(Path.Combine(AppContext.BaseDirectory,"Configs", "uploadtoimgur")))
                        File.Create(Path.Combine(AppContext.BaseDirectory,"Configs",  "uploadtoimgur"));
                    else if (File.Exists(Path.Combine(AppContext.BaseDirectory, "Configs", "uploadtoimgur")))
                        File.Delete(Path.Combine(AppContext.BaseDirectory, "Configs", "uploadtoimgur"));
                },
                GetChecked = () => File.Exists(Path.Combine(AppContext.BaseDirectory,"Configs",  "uploadtoimgur"))
            }
        };
    }

    public List<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
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
                if (Env.GetCurrentTrack != null)
                {
                    var ct = Env.GetCurrentTrack();
                    SPlay(ct.URI, ct);
                }

                break;

            case PlaybackState.Paused:
                if (Env.GetCurrentTrack != null)
                {
                    var ct = Env.GetCurrentTrack();
                    SPause(ct.URI, ct);
                }

                break;

            case PlaybackState.Buffering:
                break;
        }
    }

 
    public void TrackChangedNotification(Song newtrack)
    {
        ChangeSong(newtrack.URI, newtrack);
    }

  


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

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.DiscordRP"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/DiscordRichPresence/{typeof(DiscordRpcClient).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/Lachee/discord-rpc-csharp/issues"), URLType.LibraryCode)
    };

    public void ChangeSong(string? loc, Song a)
    {
        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        Debug.WriteLine($"cs called {artistandalbum}");

        var bigimage = GetAlbumArt(loc, a);
        SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState),
            StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
            bigimage == null ? AppName : a.Metadata?.Album ?? AppName, "start", PlayTextState);
    }

    private static string StatusOrNotToStatus(string message, string status)
    {
        if (message.Length == 0) return "";
        if (message.Length > 10) return message;
        return status + " " + message;
    }

    public string? GetAlbumArt(string? loc, Song a)
    {
        if (richPresenceURLs != null)
        {
            var url = richPresenceURLs.GetURL(a);
            if (!string.IsNullOrEmpty(url)) return url;
        }

        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        return tracks.GetValueOrDefault(artistandalbum, null);
    }

    public void SPause(string? loc, Song a)
    {
        Debug.WriteLine("Pause called");

        if (a != null)
        {
            var bigimage = GetAlbumArt(loc, a);
            SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState),
                StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PauseStateRPSMLICN, PauseTextState);
        }
        else
        {
            SetStatus(Path.GetFileNameWithoutExtension(loc), PauseTextSState, "sap", AppName, PauseStateRPSMLICN,
                PauseTextState);
        }
    }

    public void SPlay(string? loc, Song a)
    {
        Debug.WriteLine("play called");

        if (a != null)
        {
            var bigimage = GetAlbumArt(loc, a);
            SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PlayTextSState),
                StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PlayStateRPSMLICN, PlayTextState);
        }
        else
        {
            SetStatus(Path.GetFileNameWithoutExtension(loc), PlayTextSState, "sap", AppName, PlayStateRPSMLICN,
                PlayTextState);
        }
    }

    public void SStop()
    {
        Debug.WriteLine("stop called");

        SetStatus(StoppedTextSState, "Not playing anything", "sap", AppName, StoppedStateRPSMLICN, StoppedTextState);
    }

    private void SetStatus(string details, string state, string largeimage, string largeimagetext, string smallimage,
        string smallimagetext)
    {
        try
        {
            client.SetPresence(new RichPresence
            {
                Details = details,
                State = state,
                Assets = new Assets
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
    IMusicStatusInterfaceListener Env;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        Env = listener;
        client.Initialize();
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        Debug.WriteLine("disable called");
        client.Deinitialize();
        client.Dispose();
    }
}

public interface IRememberRichPresenceURLs
{
    string? GetURL(Song track);
}

public class RememberRichPresenceURLsUsingImgurAndAJsonFile : IRememberRichPresenceURLs
{
    private MscArtFile[] cached;
    private readonly ApiClient client = new("d169c9264561822", "ce1616d067cb1493bc1df67b53e03660c5c02cc2");
    private readonly ImageEndpoint imageEndpoint;
    public bool Uploadit;

    public RememberRichPresenceURLsUsingImgurAndAJsonFile()
    {
        imageEndpoint = new ImageEndpoint(client, HttpClient.Client);
    }

    public string? GetURL(Song track)
    {
        Debug.WriteLine("Geturl");
        GetCache();
        Debug.WriteLine("uplodit " + Uploadit);
        if (track != null)
        {
            var a = track.Metadata?.Pictures?.FirstOrDefault(x => x.Data.Length > 1000);
            if (a == null) return null;
            if (cached.Any(x => x.hsh == a.Hash))
                return cached.First(x => x.hsh == a.Hash).url.Replace("https", "http");

            if (Uploadit)
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
        Debug.WriteLine("uploading " + bits.Length);


        var imageUpload = imageEndpoint.UploadImageAsync(new MemoryStream(bits));
        var res = imageUpload.GetAwaiter().GetResult();
        Debug.WriteLine(res.Link);
        return res.Link;
    }


    private void GetCache()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory,"Configs", "musicart.json")))
            cached = JsonSerializer.Deserialize<MscArtFile[]>(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Configs", "musicart.json")));
        if (cached == null) cached = Array.Empty<MscArtFile>();
    }

    private void SetCache()
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Configs", "musicart.json"), JsonSerializer.Serialize(cached));
    }
}

internal record class MscArtFile(string hsh, string url);
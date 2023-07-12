using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.Xml.Serialization;
using DiscordRPC;
using DiscordRPC.Logging;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;
using SilverConfig;

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
public class DiscordPlayTrackerConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [Comment("The uploader to use")]
    public Uploader Uploader { get; set; } = Uploader.None;
    [XmlIgnore] public bool _AllowedRead = true;

    [XmlIgnore] public bool AllowedToRead => _AllowedRead;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum Uploader
{
    None = 0,
    Catbox =1,
    Imgur =2
}
[Export(typeof(IMusicStatusInterface))]
public class DiscordPlayTracker : IMusicStatusInterface, IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
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
    public ObjectToRemember ConfigObject = new(Guid.Parse("85782940-db9a-404e-9f22-1cea863da536"), new DiscordPlayTrackerConfig());
    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new ObjectToRemember[] { ConfigObject };


 
    string id = "926595775574712370";
    public DiscordPlayTracker()
    {
        ConfigurableElements = new List<IConfigurableElement>
        {
            new SimpleDropDown()
            {
                GetOptions = () => new string[] { "None", "Catbox", "Imgur" },
                GetPlaceholder = ()=>"None",
                GetSelection = () =>
                {
                    if (ConfigObject.Value is DiscordPlayTrackerConfig x)
                    {
                        return x.Uploader.ToString();
                    }

                    return Uploader.None.ToString();
                },
                SetSelection = (s) =>
                {
                    if (ConfigObject.Value is not DiscordPlayTrackerConfig x) return;
                    x.Uploader = Enum.Parse<Uploader>(s);
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x,new("Uploader"));
                    ChangeImageUploader();
                }
            },
        };
    }

    public List<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }

    
    public void Dispose()
    {
        client.Dispose();
    }

    public void PlayerStateChanged(object e, PlaybackState newstate)
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

 
    public void TrackChangedNotification(object e, Song? newtrack)
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

    public bool IsStarted => _IsStarted;
    private bool _IsStarted;

    public void ChangeSong(string? loc, Song? a)
    {
        if (!_IsStarted) return;
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

    public string? GetAlbumArt(string? loc, Song? a)
    {

        if (richPresenceURLs != null)
        {
            var url = richPresenceURLs.GetURL(a, Env);
            if (!string.IsNullOrEmpty(url)) return url;
        }

        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        return tracks.GetValueOrDefault(artistandalbum, null);
    }

    public void SPause(string? loc, Song? a)
    {
        if (!_IsStarted) return;

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

    public void SPlay(string? loc, Song? a)
    {
        if (!_IsStarted) return;


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
        if (!_IsStarted) return;

        SetStatus(StoppedTextSState, "Not playing anything", "sap", AppName, StoppedStateRPSMLICN, StoppedTextState);
    }

    private void SetStatus(string details, string state, string largeimage, string largeimagetext, string smallimage,
        string smallimagetext)
    {
        if (!_IsStarted) return;

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
        _IsStarted = true;
        client = new DiscordRpcClient(id)
        {
            SkipIdenticalPresence = false,
            Logger = new DebugLogger { Level = LogLevel.Warning }
        };
        client.OnError += (_, e) => Debug.WriteLine($"An error occurred with Discord RPC Client: {e.Code} {e.Message}");

        client.Initialize();
        listener.PlayerStateChanged += PlayerStateChanged;
        listener.TrackChangedNotification += TrackChangedNotification;
        if (ConfigObject.Value is not DiscordPlayTrackerConfig) return;
        ChangeImageUploader();
    }
    public void ChangeImageUploader()
    {
        if(ConfigObject.Value is DiscordPlayTrackerConfig x)
        {
            switch (x.Uploader)
            {
                case Uploader.None:
                    richPresenceURLs = null;
                    break;
                case Uploader.Catbox:
                    richPresenceURLs = new RememberRichPresenceURLsUsingCatboxAndAJsonFile { Uploadit = true };
                    break;
                case Uploader.Imgur:
                    richPresenceURLs = new RememberRichPresenceUrLsUsingImgurAndAJsonFile { Uploadit = true };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
    }
    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        Debug.WriteLine("disable called");
        _IsStarted = false;
        listener.PlayerStateChanged -= PlayerStateChanged;
        listener.TrackChangedNotification -= TrackChangedNotification;
        try
        {
            client.Deinitialize();
            client.Dispose();
        }
        catch
        {
        }
    }
}

internal record class MscArtFile(string hsh, string url);

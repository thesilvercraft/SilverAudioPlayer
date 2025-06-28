using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.Xml.Serialization;
using DiscordRPC;
using SilverAudioPlayer.Shared;
using SilverConfig;
using SilverCraft.Ekorensis;
using ILogger = DiscordRPC.Logging.ILogger;
using LogLevel = DiscordRPC.Logging.LogLevel;

namespace SilverAudioPlayer.DiscordRP;

public class SerilogLogger : ILogger
{
    private Serilog.ILogger? _logger;

    public SerilogLogger()
    {
        _logger = Logger.GetLogger(typeof(SerilogLogger));
    }

    public void Trace(string message, params object[] args)
    {
        _logger?.Verbose(message, args);
    }

    public void Info(string message, params object[] args)
    {
        _logger?.Information(message, args);
    }


    public void Warning(string message, params object[] args)
    {
        _logger?.Warning(message, args);
    }


    public void Error(string message, params object[] args)
    {
        _logger?.Error(message, args);
    }

    public LogLevel Level { get; set; }
}

public class DiscordPlayTrackerConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }

    [Comment("The uploader to use")] public Uploader Uploader { get; set; } = Uploader.None;

    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum Uploader
{
    None = 0,
    Catbox = 1,
    Imgur = 2
}

[Export(typeof(IMusicStatusInterface))]
public class DiscordPlayTracker : IMusicStatusInterface, IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
{
    private const string AppName = "Catenoid";

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

    public ObjectToRemember ConfigObject =
        new(Guid.Parse("85782940-db9a-404e-9f22-1cea863da536"), new DiscordPlayTrackerConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new[] { ConfigObject };


    string id = "926595775574712370";
    private static readonly string[] optionsForUploader = new string[] { "None", "Catbox", "Imgur" };

    public DiscordPlayTracker()
    {
        Debug.WriteLine("New instance of discordplaytracker");
        ConfigurableElements = new List<IConfigurableElement>
        {
            new SimpleDropDown()
            {
                GetOptions = () => optionsForUploader,
                GetPlaceholder = () => "None",
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
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("Uploader"));
                    ChangeImageUploader();
                }
            },
        };
    }

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }


    public void Dispose()
    {
        client?.Dispose();
        GC.SuppressFinalize(this);
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
                    SPlay(ct);
                }

                break;

            case PlaybackState.Paused:
                if (Env.GetCurrentTrack != null)
                {
                    var ct = Env.GetCurrentTrack();
                    SPause(ct);
                }

                break;

            case PlaybackState.Buffering:
                break;
        }
    }


    public void TrackChangedNotification(object e, Song? newtrack)
    {
        ChangeSong(newtrack);
    }

    public string Name => "Discord RP";

    public string Description => "Discord rich presence music status interface";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(DiscordPlayTracker).Assembly,
        "SilverAudioPlayer.Any.MusicStatusInterface.DiscordRP.drp.svg");

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
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.DiscordRP"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/DiscordRichPresence/{typeof(DiscordRpcClient).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/Lachee/discord-rpc-csharp/issues"), URLType.LibraryCode)
    };

    public bool IsStarted => _IsStarted;
    private bool _IsStarted;

    public void ChangeSong(Song? a)
    {
        if (!_IsStarted) return;
        var bigimage = GetAlbumArt(a);
        SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState),
            StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
            bigimage == null ? AppName : a.Metadata?.Album ?? AppName, "start", PlayTextState);
    }

    private static string StatusOrNotToStatus(string message, string status)
    {
        return message.Length switch
        {
            0 => "",
            > 10 => message,
            _ => status + " " + message
        };
    }

    public string? GetAlbumArt(Song? a)
    {
        if (richPresenceURLs != null)
        {
            var url = richPresenceURLs.GetURL(a, Env);
            if (!string.IsNullOrEmpty(url)) return url;
        }

        var artistandalbum = $"{a.Metadata?.Album} - {a.Metadata?.Artist}";
        return tracks.GetValueOrDefault(artistandalbum, null);
    }

    private void SPause(Song? a)
    {
        if (!_IsStarted) return;
        if (a != null)
        {
            if (a.Metadata != null)
            {
                var bigimage = GetAlbumArt(a);
                SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PauseTextSState),
                    StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                    bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PauseStateRPSMLICN, PauseTextState);
            }
            else
            {
                SetStatus(Path.GetFileNameWithoutExtension(a.URI), PauseTextSState, "sap", AppName, PauseStateRPSMLICN,
                    PauseTextState);
            }
        }
    }

    private void SPlay(Song? a)
    {
        if (!_IsStarted) return;

        if (a != null)
        {
            if (a.Metadata != null)
            {
                var bigimage = GetAlbumArt(a);
                SetStatus(StatusOrNotToStatus(a.Metadata?.Title ?? a.Name, PlayTextSState),
                    StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                    bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PlayStateRPSMLICN, PlayTextState);
            }
            else
            {
                SetStatus(Path.GetFileNameWithoutExtension(a.URI), PlayTextSState, "sap", AppName, PlayStateRPSMLICN,
                    PlayTextState);
            }
        }
        else
        {
            SStop();
        }

    }

    private void SStop()
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
                Type = ActivityType.Listening,
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
        client = new DiscordRpcClient(id)
        {
            SkipIdenticalPresence = false,
            Logger = new SerilogLogger(),
        };
        _IsStarted = client.Initialize();
        if (!_IsStarted)
        {
            StopIPC(listener);
            return;
        }
        listener.PlayerStateChanged += PlayerStateChanged;
        listener.TrackChangedNotification += TrackChangedNotification;
        if (ConfigObject.Value is not DiscordPlayTrackerConfig) return;
        ChangeImageUploader();
    }

    public void ChangeImageUploader()
    {
        if (ConfigObject.Value is DiscordPlayTrackerConfig x)
        {
            richPresenceURLs = x.Uploader switch
            {
                Uploader.None => null,
                Uploader.Catbox => new RememberRichPresenceURLsUsingCatboxAndAJsonFile { Uploadit = true },
                Uploader.Imgur => new RememberRichPresenceUrLsUsingImgurAndAJsonFile { Uploadit = true },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
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
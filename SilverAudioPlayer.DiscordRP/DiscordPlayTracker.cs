using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using DiscordRPC;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;
using ILogger = DiscordRPC.Logging.ILogger;
using LogLevel = DiscordRPC.Logging.LogLevel;

namespace SilverAudioPlayer.DiscordRP;

public class SerilogLogger : ILogger
{
  

    public void Trace(string message, params object[] args)
    {
        Log.Verbose(message, args);
    }

    public void Info(string message, params object[] args)
    {
        Log.Information(message, args);
    }


    public void Warning(string message, params object[] args)
    {
        Log.Warning(message, args);
    }


    public void Error(string message, params object[] args)
    {
        Log.Error(message, args);
    }

    public LogLevel Level { get; set; }
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
    private const string PausedSmallIcon = "pause";

    private const string PlayTextState = "Playing";
    private const string PlayingSmallIcon = "start";

    private const string StoppedTextState = "Stopped";
    private const string StoppedSmallIcon = "stop";

    private DiscordRpcClient? _client;

    private readonly List<IConfigurableElement> ConfigurableElements;
    public IRememberRichPresenceURLs? richPresenceURLs;

    public ObjectToRemember ConfigObject =
        new(Guid.Parse("85782940-db9a-404e-9f22-1cea863da536"), new DiscordPlayTrackerConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new[] { ConfigObject };


    string id = "926595775574712370";
    private static readonly string[] optionsForUploader = new string[] { "None", "Catbox", "Imgur" };
    private IConfigPathProvider _configPathProvider;
    private HttpClient _httpClient;
    public DiscordPlayTracker(IConfigPathProvider configPathProvider, HttpClient httpClient)
    {
        _configPathProvider = configPathProvider;
        _httpClient = httpClient;
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
                    return nameof(Uploader.None);
                },
                SetSelection = s =>
                {
                    if (ConfigObject.Value is not DiscordPlayTrackerConfig x) return;
                    x.Uploader = Enum.Parse<Uploader>(s);
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("Uploader"));
                    ChangeImageUploader();
                }
            },
           new SimpleRow
            {

                Content =
               [
                   new SimpleLabel
                   {
                       GetContent = ()=>"Enable custom button",
                   },
                   new SimpleCheckBox
                   {
                       Checked = b =>  {
                           if (ConfigObject.Value is not DiscordPlayTrackerConfig x) return;
                           x.CustomButtonEnabled = b;
                           ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new PropertyChangedEventArgs(nameof(x.CustomButtonEnabled)));
                       },
                       GetContent = ()=>"",
                       Toggled = ConfigObject.Value is DiscordPlayTrackerConfig { CustomButtonEnabled: true },
                       GetChecked = () => ConfigObject.Value is DiscordPlayTrackerConfig { CustomButtonEnabled: true }
                   }
               ]
            },
            new SimpleTextBox
            { 
                GetContent = () => ((DiscordPlayTrackerConfig)ConfigObject.Value).CustomButtonLabel,
                GetPlaceholder= () => "Button label",
                SetContent = c =>
                {
                    if (ConfigObject.Value is not DiscordPlayTrackerConfig x) return;
                    x.CustomButtonLabel = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("CustomButtonLabel"));
                },
            },
            new SimpleTextBox
            { 
                GetContent = () => ((DiscordPlayTrackerConfig)ConfigObject.Value).CustomButtonUrl,
                GetPlaceholder= () => "Button url",
                SetContent = c =>
                {
                    if (ConfigObject.Value is not DiscordPlayTrackerConfig x) return;
                    x.CustomButtonUrl = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("CustomButtonUrl"));
                },
            },
        };
    }

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return ConfigurableElements;
    }


    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void PlayerStateChanged(object? e, PlaybackState newstate)
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
            default:
                break;
        }
    }


    private void SongChanged(object? e, Song? song)
    {
        if (!IsStarted) return;
        SPlay(song);
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
            new Uri(ConstantCodeInfo.GetCurrentUrl()),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/DiscordRichPresence/{typeof(DiscordRpcClient).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/Lachee/discord-rpc-csharp"), URLType.LibraryCode)
    };

    public bool IsStarted { get; private set; }


    private static string StatusOrNotToStatus(string message, string status)
    {
        return message.Length switch
        {
            0 => "",
            > 10 => message,
            _ => status + " " + message
        };
    }

    private string? GetAlbumArt(Song? a)
    {
        if (richPresenceURLs == null) return null;
        var url = richPresenceURLs.GetURL(a, Env);
        return !string.IsNullOrEmpty(url) ? url : null;
    }

    private void SPause(Song? a)
    {
        if (!IsStarted) return;
        if (a == null) return;
        if (a.Metadata != null)
        {
            var bigimage = GetAlbumArt(a);
            SetStatus(a.Metadata?.Title ?? a.Name,
                StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PausedSmallIcon, PauseTextState);
        }
        else
        {
            SetStatus(Path.GetFileNameWithoutExtension(a.URI), PauseTextState, "sap", AppName, PausedSmallIcon,
                PauseTextState);
        }
    }

    public static void TimerCallback(object? state)
    {
        if (state is DiscordPlayTracker dpt)
        {
            dpt.UpdateTime();
        }
    } 
    void UpdateTime()
    {
        if(Env.GetState()!=PlaybackState.Playing) return;
        _currentPresence.Timestamps =  new Timestamps
        {
            Start = DateTime.Now.AddSeconds(-Env.GetPosition()).ToUniversalTime(),
            End = DateTime.Now.AddSeconds(Env.GetDuration()-Env.GetPosition()).ToUniversalTime()
        };
        _client.SetPresence(_currentPresence);
    }

    private void SPlay(Song? a)
    {
        if (!IsStarted) return;
        
        if (a != null)
        {
            if (a.Metadata != null)
            {
                var bigimage = GetAlbumArt(a);
                SetStatus(a.Metadata?.Title ?? a.Name,
                    StatusOrNotToStatus(a.Metadata?.Artist ?? "unknown", "by"), bigimage ?? "sap",
                    bigimage == null ? AppName : a.Metadata?.Album ?? AppName, PlayingSmallIcon, PlayTextState,
                    DateTime.Now.AddSeconds(-Env.GetPosition()).ToUniversalTime(),
                    DateTime.Now.AddSeconds(Env.GetDuration()-Env.GetPosition()).ToUniversalTime());
            }
            else
            {
                SetStatus(Path.GetFileNameWithoutExtension(a.URI), PlayTextState, "sap", AppName, PlayingSmallIcon,
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
        if (!IsStarted) return;

        SetStatus(StoppedTextState, "Not playing anything", "sap", AppName, StoppedSmallIcon, StoppedTextState, appName:true);
        
    }

    private RichPresence _currentPresence = new();

    private void SetStatus(string details, string state, string largeimage, string largeimagetext, string smallimage,
        string smallimagetext, DateTime? start = null, DateTime? end = null, bool appName = false)
    {
        if (!IsStarted) return;

        try
        {
            Timestamps? x = null;
            if (start != null || end!=null)
            {
                x = new Timestamps
                {
                    Start = start,
                    End = end
                };
            }
            Button[] buttons=[];
            if (ConfigObject.Value is DiscordPlayTrackerConfig { CustomButtonEnabled: true } conf)
            {
                buttons = [ new Button()
                {
                    Label =conf.CustomButtonLabel,
                    Url = conf.CustomButtonUrl,
                }];
                if (appName)
                {
                    buttons = [..buttons, new Button()
                    {
                        Label = "SilverAudioPlayer",
                        Url = ConstantCodeInfo.GitRepo
                    }];
                }
            }
            else if (appName)
            {
                buttons = [new Button()
                {
                    Label = "SilverAudioPlayer",
                    Url = ConstantCodeInfo.GitRepo
                }];
            }
            _currentPresence = new RichPresence
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
                },
                StatusDisplay = appName ? StatusDisplayType.Name : StatusDisplayType.Details,
                Timestamps = x,
                Buttons = buttons
            };
            _client.SetPresence(_currentPresence);
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
        _client = new DiscordRpcClient(id)
        {
            SkipIdenticalPresence = false,
            Logger = new SerilogLogger(),
        };
        IsStarted = _client.Initialize();
        if (!IsStarted)
        {
            StopIPC(listener);
            return;
        }
        listener.PlayerStateChanged += PlayerStateChanged;
        listener.TrackChangedNotification += SongChanged;
        listener.PositionChanged += Seeked;
        if (ConfigObject.Value is not DiscordPlayTrackerConfig) return;
        ChangeImageUploader();
    }

   
    private void Seeked(object? sender, TimeSpan e)
    {
        if (!IsStarted) return;
        UpdateTime();

    }

    private void ChangeImageUploader()
    {
        if (ConfigObject.Value is DiscordPlayTrackerConfig x)
        {
            richPresenceURLs = x.Uploader switch
            {
                Uploader.None => null,
                Uploader.Catbox => new RememberRichPresenceURLsUsingCatboxAndAJsonFile(_configPathProvider, _httpClient) { Uploadit = true },
                Uploader.Imgur => new RememberRichPresenceUrLsUsingImgurAndAJsonFile(_configPathProvider,_httpClient) { Uploadit = true },
                _ => throw new ArgumentOutOfRangeException(nameof(x.Uploader), x.Uploader, "The uploader type is unknown to this plugin")
            };
        }
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        IsStarted = false;
        listener.PlayerStateChanged -= PlayerStateChanged;
        listener.TrackChangedNotification -= SongChanged;
        listener.PositionChanged -= Seeked;
        try
        {
            _client.Deinitialize();
            _client.Dispose();
        }
        catch (Exception e)
        {
            Log.Error(e,$"Error during StopIPC on {nameof(DiscordPlayTracker)}");
        }
    }
}

internal record class MscArtFile(string hsh, string url);
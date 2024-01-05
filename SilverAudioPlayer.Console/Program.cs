using System.Collections;
using System.Diagnostics;
using ClangSharp;
using Opportunity.LrcParser;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using Silver.Composition;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;
using Swordfish.NET.Collections;
using Logger = SilverAudioPlayer.Shared.Logger;
namespace SilverAudioPlayer.Console;
public class RxObservableExceptionHandler : IObserver<Exception>
{
    public void OnNext(Exception value)
    {
        Log.Error(value,
            "Unhandled exception caught by RxObservableExceptionHandler.OnNext");
    }

    public void OnError(Exception error)
    {
        Log.Error(error,
            "Unhandled exception caught by RxObservableExceptionHandler.OnError");
    }

    public void OnCompleted()
    {

    }
}
[Serializable]
public class ProvidersReturnedNullException : Exception
{
    public ProvidersReturnedNullException()
    {
    }

    public ProvidersReturnedNullException(string message) : base(message)
    {
    }

    public ProvidersReturnedNullException(string message, Exception inner) : base(message, inner)
    {
    }

}
public class Context : PlayerContext
{
    private string _Title;
    public ConcurrentObservableCollection<Song> queue = [];

    public string Title
    {
        get => _Title;
        set => this.RaiseAndSetIfChanged(ref _Title, value);
    }

}
public class ConsoleEnviroment : IHaveConfigFilesWithKnownLocations, IFullPlayerEnvironment
{

    public event EventHandler<Song> TrackChangedNotification;
    public event EventHandler<PlaybackState> PlayerStateChanged;

    public ConsoleEnviroment(Logic<Context> Logic)
    {
        this.Logic = Logic;
        this.context = Logic.PlayerContext;
    }
    Context context;
    Logic<Context> Logic;
    public void LoadSong(WrappedStream s)
    {
        Task.Run(() => Logic.ProcessStream(s));
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        Logic.ProcessFiles(files);
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        Logic.ProcessStreams(streams);
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return Logic.FilterFiles(files);
    }

    public string Name => "SilverAudioPlayer.Consol";

    public string Description => "console UI for SilverAudioPlayer";

    public WrappedStream? Icon => null;

    public Version? Version => typeof(ConsoleEnviroment).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Console"),
            URLType.Code),
    ];

    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream)
    {
        return Logic.GetMetadataFromStream(stream);
    }


    public async Task<List<Song>?> GetQueue()
    {
        //TODO ASK USER BEFORE GIVING OVER QUEUE
        return Logic.GetQueueCopy();
    }

    public IPicture? GetBestRepresentation(IReadOnlyList<IPicture>? pictures, PictureType type = PictureType.Front)
    {
        var res = pictures?.Where(x => x.PicType == type);
        return res?.Any() == true ? res?.First() : pictures?.FirstOrDefault();
    }

    public void Play() => Logic.Play();

    public void Pause() => Logic.Pause();

    public void PlayPause() => Logic.PlayPause(true);

    public void Stop() => Logic.RemoveTrack();

    public void Next() => Logic.Next();

    public void Previous() => Logic.Previous();

    public void SetVolume(byte volume) => context.Volume = volume;

    public byte GetVolume() => context.Volume;

    public Song? GetCurrentTrack()
    {
        return context.CurrentSong;
    }

    public double GetDuration() => Logic.Player?.Length()?.TotalSeconds ??
                                   (context.CurrentSong?.Metadata?.Duration / 1000) ?? 2;


    public void SetPosition(double position) => Logic.Player?.SetPosition(TimeSpan.FromSeconds(position));

    public double GetPosition() => Logic.Player?.GetPosition().TotalSeconds ?? 1;

    public PlaybackState GetState()
    {
        var x = Logic.Player?.GetPlaybackState();
        if (x != null)
        {
            return (PlaybackState)x;
        }

        return PlaybackState.Stopped;
    }

    public RepeatState GetRepeat() => context.LoopType;

    public void SetRepeat(RepeatState state) => context.LoopType = state;

    public bool GetShuffle()
    {
        return false;
    }

    public void SetShuffle(bool shuffle)
    {
    }

    public void SetRating(byte rating)
    {
        //A music player should probably not edit the metadata of the music it plays
        //If someone thinks otherwise feel free to add code to this method
    }


    public string GetLyrics()
    {
        return context.CurrentSong?.Metadata?.Lyrics;
    }

    public void FireTrackChangedNotification(Song? currentSong)
    {
        Task.Run(() => { TrackChangedNotification?.Invoke(this, currentSong); });
    }

    public void FirePlayerStateChanged(PlaybackState state)
    {
        Task.Run(() => { PlayerStateChanged?.Invoke(this, state); });
    }


    public string Licenses => @"SilverAudioPlayer.Console
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
";

    public string[] KnownConfigFileLocations =>
    [
        ConfigPath.GetPath("SilverAudioPlayer.Config.xml"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilverCraftAvaloniav1Shared", "dotfile.json")
    ];

    public IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements)
    {
        // EkonrensisWindow window = new();
        //window.HandleConfiguration(elements);
        return null;
    }
}
public class App
{

    public static void Main(string[] args)
    {
        Config mwconfig;
        CommentXmlConfigReaderNotifyWhenChanged<Config> mwconfreader;
        string _configPath;
        var confdir = Path.Combine(AppContext.BaseDirectory, "Configs");
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, ".portable")))
        {
            if (OperatingSystem.IsLinux())
            {
                confdir = Path.Combine(
                    Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                    Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".config"), "SilverAudioPlayer");
            }
            else if (OperatingSystem.IsWindows())
            {
                confdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SilverCraft", "SilverAudioPlayer");
            }
        }

        if (!Directory.Exists(confdir))
        {
            Directory.CreateDirectory(confdir);
        }

        ConfigPath.Set(confdir);
        _configPath = ConfigPath.GetPath("SilverAudioPlayer.Config.xml");
        mwconfreader = new CommentXmlConfigReaderNotifyWhenChanged<Config>();
        Shared.HttpClient.Client.DefaultRequestHeaders.Add("User-Agent", "SilverAudioPlayer/6.0 (+https://gitlab.com/silvercraft/SilverAudioPlayer)");
        if (!File.Exists(_configPath)) mwconfreader.Write(new Config(), _configPath);
        mwconfig = mwconfreader.Read(_configPath) ?? new Config();
        if (mwconfig.UserLogPreferences == ConfigStyle.Unknown)
        {
            //todo: show user dialog to choose
        }
        //todo: logging based on config
        var logger = new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug)
            .CreateLogger();
        Log.Logger = logger;
        void OnFatalError(Exception y)
        {
            if (File.Exists("/usr/bin/kdialog"))
            {
                Process.Start("kdialog", $"--desktopfile silveraudioplayer.desktop --title \"Fatal error\" --detailederror \"An unexpected error occured and SilverAudioPlayer was forced to close\nPlease report this issue on gitlab.\" \"{y.ToString()}\"");
            }
        }
        AppDomain.CurrentDomain.UnhandledException += (x, y) =>
        {
            if (y.IsTerminating)
            {
                Log.Fatal((Exception)y.ExceptionObject,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException, Terminating!!!");
                OnFatalError((Exception)y.ExceptionObject);
            }
            else
            {
                Log.Error((Exception)y.ExceptionObject,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (x, y) =>
        {
            Log.Fatal(y.Exception,
                "Unhandled exception caught by TaskScheduler.UnobservedTaskException, Terminating!!!");
            OnFatalError(y.Exception);
        };
        RxApp.DefaultExceptionHandler = new RxObservableExceptionHandler();
        Logger.GetLoggerFunc += logger.ForContext;
        Context context = new();
        Logic<Context> Logic = new(context);
        ConsoleEnviroment env = new(Logic);
        Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
        PlatformLogicHelper.SatisfyImports(Logic);
        if (Logic.PlayProviders == null)
            throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
        Logic.PlayableMimes = [];
        var canProvideMemory = Logic.MemoryProvider != null;
        if (!canProvideMemory)
        {
            logger.Warning("Failed to find a memory provider, proceeding anyways");
        }

        foreach (var provider in Logic.PlayProviders)
        {
            var name = provider.GetType().Name;
            Debug.WriteLine($"Play provider {name} loaded.");
            if (canProvideMemory && provider is IAmOnceAgainAskingYouForYourMemory asker)
            {
                Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
            }

            if (provider.SupportedMimes != null) Logic.PlayableMimes.AddRange(provider.SupportedMimes);
        }

        var playproviderloadtask = Task.Run(async () =>
        {
            foreach (var playprovider in Logic.PlayProviders)
                if (playprovider != null)
                    await playprovider.OnStartup();
        });


        void HandleMemoryLogic(IEnumerable objectsOwnedByLogic)
        {
            foreach (var o in objectsOwnedByLogic)
            {
                if (canProvideMemory && o is IAmOnceAgainAskingYouForYourMemory asker)
                {
                    Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                }
            }
        }

        if (canProvideMemory)
        {
             HandleMemoryLogic(((ILogicImportable)Logic).PluginCodeInformation);
        }
        Logic.PlayerEnvironment = env;
        Logic.Log = logger;
        if (args.Length == 0)
        {
            //args = ["https://www.youtube.com/watch?v=qXwqaRHr3hg"];
        }
        Logic.ProcessFiles(args);
        System.Console.WriteLine("Ayo");
        System.Console.Clear();
        Logic.MainWindow_Opened(env);
        Logic.StartPlaying();
        while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
        {
            while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
            {
                var a = System.Console.GetCursorPosition();
                System.Console.SetCursorPosition(0, 0);
                for (int y = 0; y < a.Top; y++)
                {
                    for (int x = 0; x < 80; x++)
                    {
                        System.Console.Write(' ');

                    }
                    System.Console.Write('\n');
                }
                System.Console.SetCursorPosition(0, 0);

                System.Console.WriteLine($"{Logic.Player?.GetPlaybackState()} {Logic.Player?.GetPosition():g}/{Logic.Player?.Length():g} ({(Logic.Player?.GetPosition() / Logic.Player?.Length()) * 100:00.00}%) {context.CurrentSong?.TitleOrURLF}");
                if (context?.CurrentSong?.Metadata?.SyncedLyrics is IList<LyricPhrase> lyrics && lyrics.Count > 1)
                {
                    int last = 0;
                    for (int i = 0; i < lyrics.Count; i++)
                    {
                        LyricPhrase? l = lyrics[i];
                        if (l.TimeStampInMilliSeconds <= Logic.Player.GetPosition().TotalMilliseconds)
                        {
                            last = i;
                        }
                        else
                        {
                            break;
                        }
                    }
                     if (last > 4)
                    {
                        System.Console.WriteLine(lyrics[last - 4].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 3].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 2].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 1].ToString().ReplaceLineEndings(""));
                    }
                    else if (last > 3)
                    {
                        System.Console.WriteLine(lyrics[last - 3].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 2].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 1].ToString().ReplaceLineEndings(""));
                    }
                     else if (last > 2)
                    {
                        System.Console.WriteLine(lyrics[last - 2].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last - 1].ToString().ReplaceLineEndings(""));
                    }
                    else if (last > 1)
                    {
                        System.Console.WriteLine(lyrics[last - 1].ToString().ReplaceLineEndings(""));
                    }
                    System.Console.WriteLine("\x1b[96m" + lyrics[last].ToString().ReplaceLineEndings("") + "\u001b[0m");
                    if (last < lyrics.Count - 3)
                    {
                        System.Console.WriteLine(lyrics[last + 1].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last + 2].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last + 3].ToString().ReplaceLineEndings(""));


                    }
                    if (last < lyrics.Count - 2)
                    {
                        System.Console.WriteLine(lyrics[last + 1].ToString().ReplaceLineEndings(""));
                        System.Console.WriteLine(lyrics[last + 2].ToString().ReplaceLineEndings(""));

                    }
                    else if (last < lyrics.Count - 1)
                    {
                        System.Console.WriteLine(lyrics[last + 1].ToString().ReplaceLineEndings(""));
                    }
                }
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.A:
                            System.Console.Write("Enter URL to add to queue:");
                            var r = System.Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(r))
                            {
                                Logic.ProcessFiles([r.Trim('\'', '\"', ' ')]);
                            }
                            break;
                        case ConsoleKey.RightArrow:
                            env.SetPosition(env.GetPosition() + 5);
                            break;
                        case ConsoleKey.LeftArrow:
                            env.SetPosition(env.GetPosition() - 5);
                            break;
                        case ConsoleKey.OemComma:
                            env.Previous();
                            break;
                        case ConsoleKey.OemPeriod:
                            env.Next();
                            break;
                        case ConsoleKey.UpArrow:
                            env.SetVolume((byte)Math.Clamp(env.GetVolume() + 5, 0, 100));
                            break;
                        case ConsoleKey.DownArrow:
                            env.SetVolume((byte)Math.Clamp(env.GetVolume() - 5, 0, 100));
                            break;
                        case ConsoleKey.Spacebar:
                            env.PlayPause();
                            break;
                        case ConsoleKey.Q:
                            Environment.Exit(0);
                            break;
                        default:
                            break;
                    }
                }
                Thread.Sleep(90);
            }
            Thread.Sleep(2000);

        }
    }

}

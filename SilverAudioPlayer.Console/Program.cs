using System.Collections;
using System.Diagnostics;
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
    private static BootupLogic BootupLogic = new();

    public static void Main(string[] args)
    {
        Config mwconfig;
        CommentXmlConfigReaderNotifyWhenChanged<Config> mwconfreader;
        string _configPath;
        BootupLogic.ConfigureConfigPath();
        _configPath = ConfigPath.GetPath("SilverAudioPlayer.Config.xml");
        mwconfreader = new CommentXmlConfigReaderNotifyWhenChanged<Config>();
        BootupLogic.ConfigureHttpClient();
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
        BootupLogic.SetupCrashFallNets((Exception y) =>
            {
                if (File.Exists("/usr/bin/kdialog"))
                {
                    Process.Start("kdialog", $"--desktopfile silveraudioplayer.desktop --title \"Fatal error\" --detailederror \"An unexpected error occured and SilverAudioPlayer was forced to close\nPlease report this issue on gitlab.\" \"{y.ToString()}\"");
                }
            });

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

            if (provider.SupportedMimes != null) Logic.PlayableMimes.AddRange(provider.SupportedMimes);
        }

        var playproviderloadtask = Task.Run(async () =>
        {
            foreach (var playprovider in Logic.PlayProviders)
                if (playprovider != null)
                    await playprovider.OnStartup();
        });


        BootupLogic.SetupMemoryLogic(Logic);

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
        int prevsize=System.Console.WindowHeight;
        while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
        {
            while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
            {
                var (Left, Top) = System.Console.GetCursorPosition();
                System.Console.SetCursorPosition(0, 0);
                for (int y = 0; y < Top; y++)
                {
                    for (int x = 0; x < 80; x++)
                    {
                        System.Console.Write(' ');

                    }
                    System.Console.Write('\n');
                }
                System.Console.SetCursorPosition(0, 0);
                if(prevsize!=System.Console.WindowHeight)
                {
                    System.Console.Clear();
                    prevsize=System.Console.WindowHeight;
                }
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
                    int recommendedI=(System.Console.WindowHeight-2)/2;
                    if(recommendedI==0) goto SLyric2;

                    for (int i = recommendedI; i > 0; i--)
                    {
                        if (last > i)
                        {
                            for (int l = i; l > 0; l--)
                            {
                                System.Console.WriteLine(lyrics[last - l].ToString().ReplaceLineEndings(""));

                            }
                            goto SLyric1;

                        }
                    }
                SLyric1:

                    System.Console.WriteLine("\x1b[96m" + lyrics[last].ToString().ReplaceLineEndings("") + "\u001b[0m");
                    for (int i = recommendedI; i > 0; i--)
                    {
                        if (last < lyrics.Count - i)
                        {
                            for (int l = 1; l <= i; l++)
                            {
                                System.Console.WriteLine(lyrics[last + l].ToString().ReplaceLineEndings(""));

                            }
                            goto SLyric2;

                        }
                    }
                SLyric2:;

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


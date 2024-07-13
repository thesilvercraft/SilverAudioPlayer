using System.Diagnostics;
using Serilog;
using Serilog.Events;
using Silver.Composition;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using Logger = SilverAudioPlayer.Shared.Logger;
namespace SilverAudioPlayer.Console;

public class App
{
    private static BootupLogic BootupLogic = new();

    public static void Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.Unicode;
        System.Console.InputEncoding = System.Text.Encoding.Unicode;
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
            //TODO: show user dialog to choose
        }
        //TODO: logging based on config
        var logger = new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug)
            .CreateLogger();
        Log.Logger = logger;
        BootupLogic.SetupCrashFallNets(System.Console.Error.WriteLine);
        Logger.GetLoggerFunc += logger.ForContext;
        Context context = new();
        Logic<Context> Logic = new(context);
        ConsoleEnvironment env = new(Logic);
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
        foreach (var metadataGatherer in Logic.MetadataGatherers)
        {
            if (metadataGatherer is IMetadataPictureOptOut s)
            {
                s.StopLoadingPictures();
            }
        }
        foreach (var metadataProvider in Logic.MetadataProviders)
        {
            if (metadataProvider is IMetadataPictureOptOut s)
            {
                s.StopLoadingPictures();
            }
        }
        BootupLogic.SetupMemoryLogic(Logic);

        Logic.PlayerEnvironment = env;
        Logic.Log = logger;
        Logic.ProcessFiles(args);
        Logic.MainWindow_Opened(env);
        Logic.StartPlaying();
        int prevsize=System.Console.WindowHeight;
        while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
        {
            while (Logic.Player?.GetPlaybackState() != PlaybackState.Stopped)
            {
                var  Top = System.Console.CursorTop;
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
                System.Console.WriteLine($"{Logic.Player?.GetPlaybackState()} {Logic.Player?.GetPosition():hh\\:mm\\:ss\\.ff}/{Logic.Player?.Length():hh\\:mm\\:ss\\.ff} ({Logic.Player?.GetPosition() / Logic.Player?.Length() * 100:00.00}%) {context.CurrentSong?.TitleOrURLF}");
                if (context?.CurrentSong?.Metadata?.SyncedLyrics is { } lyrics && lyrics.Count > 1)
                {
                    //TODO: split up lyrics by line
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


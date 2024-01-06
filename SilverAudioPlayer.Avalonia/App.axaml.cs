using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Silver.Composition;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekonrensis.Avalonia;
using SilverCraft.Ekorensis;
using SilverCraft.Shared;
using Logger = SilverAudioPlayer.Shared.Logger;

namespace SilverAudioPlayer.Avalonia;

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

public class GuiSink : ILogEventSink
{
    public StringBuilder writer = new();
    public event Action<string>? New;
    public void Emit(LogEvent logEvent)
    {
        if (writer.Length > 25 * 1000)
        {
            writer.Clear();
        }

        var render = logEvent.RenderMessage() + '\n';
        writer.AppendLine(render);
        New?.Invoke(render);
    }
}
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
public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public static Config mwconfig;
    public static CommentXmlConfigReaderNotifyWhenChanged<Config> mwconfreader;
    public static string _configPath;
    public static ComandPalette ComandPalette = new();
    public static MainWindow mainWindow;
    public static GuiSink sink = new();
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
            HttpClient.Client.DefaultRequestHeaders.Add("User-Agent", "SilverAudioPlayer/6.0 (+https://gitlab.com/silvercraft/SilverAudioPlayer)");
            if (!File.Exists(_configPath)) mwconfreader.Write(new Config(), _configPath);
            mwconfig = mwconfreader.Read(_configPath) ?? new Config();
            if (mwconfig.UserLogPreferences == ConfigStyle.Unknown)
            {
                //todo: show user dialog to choose
            }
            //todo: logging based on config
            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink, LogEventLevel.Debug)
                .WriteTo.Debug(LogEventLevel.Debug)
                .CreateLogger();
            LogWindow lw = new();
            lw.Show();
            Log.Logger = logger;
            void OnFatalError(Exception y)
            {
                var a = Path.GetTempFileName();
                File.WriteAllText(a, sink.writer.ToString());
                if (File.Exists("/usr/bin/kdialog"))
                {
                    Process.Start("kdialog", $"--desktopfile silveraudioplayer.desktop --title \"Fatal error\" --detailederror \"An unexpected error occured and SilverAudioPlayer was forced to close\nPlease report this issue on gitlab.\" \"A more detailed account on what happened is saved in {a}\n{y.ToString()}\"");
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

            var mw = new MainWindow();
            mainWindow = mw;
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            desktop.MainWindow = mw;
            PlatformLogicHelper.SatisfyImports(mw.Logic);
            if (mw.Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            mw.Logic.PlayableMimes = [];
            var canProvideMemory = mw.Logic.MemoryProvider != null;
            if (!canProvideMemory)
            {
                logger.Warning("Failed to find a memory provider, proceeding anyways");
            }

            foreach (var provider in mw.Logic.PlayProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Play provider {name} loaded.");
                if (canProvideMemory && provider is IAmOnceAgainAskingYouForYourMemory asker)
                {
                    mw.Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                }

                if (provider.SupportedMimes != null) mw.Logic.PlayableMimes.AddRange(provider.SupportedMimes);
            }

            var playproviderloadtask = Task.Run(async () =>
            {
                foreach (var playprovider in mw.Logic.PlayProviders)
                    if (playprovider != null)
                        await playprovider.OnStartup();
            });
            if (canProvideMemory && mw is IAmOnceAgainAskingYouForYourMemory mainwindowMemory)
            {
                mw.Logic.MemoryProvider.RegisterObjectsToRemember(mainwindowMemory.ObjectsToRememberForMe);
            }

            void HandleMemoryLogic(IEnumerable objectsOwnedByLogic)
            {
                foreach (var o in objectsOwnedByLogic)
                {
                    if (canProvideMemory && o is IAmOnceAgainAskingYouForYourMemory asker)
                    {
                        mw.Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                    }
                }
            }
            void HandleCommandPalette(ICodeInformation item)
            {
                if (item is IAmConfigurable configurable)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                    {
                        EkonrensisWindow cw = new();
                        cw.HandleConfiguration(configurable);
                        cw.Show();
                    }));
                }
                else if (item is IConfigurableUiPlugin configurableUiPlugin)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                  {
                      configurableUiPlugin.ConfigureUi(mw.Env);
                  }));
                }
                if (item is IPlayStreamProvider streamProvider)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                  {
                      streamProvider.Use(mw.Env);
                  }));
                }
                else if (item is ISyncPlugin syncPlugin)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                 {
                     syncPlugin.Use(mw.Env);
                 }));
                }
                if (item is IMusicStatusInterface musicStatusInterface)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Toggle " + item.Name, "toggle " + item.GetType().Name, (x) =>
                 {
                     if (musicStatusInterface.IsStarted)
                     {
                         musicStatusInterface.StopIPC((IMusicStatusInterfaceListener)x);
                     }
                     else
                     {
                         musicStatusInterface.StartIPC((IMusicStatusInterfaceListener)x);
                     }
                 }));
                }
                if (item is IAmOnceAgainAskingYouForYourMemory memoryAsker && mw.Logic.MemoryProvider is IWillTellYouWhereIStoreTheConfigs l)
                {
                    ComandPalette.AddItem(new CommandPaletteItem("Open config file for " + item.Name, "config file " + item.GetType().Name, (x) =>
                 {
                     LaunchActionsWindow launchActionsWindow = new();
                     var actions = memoryAsker.ObjectsToRememberForMe.Select(ob => l.GetConfig(ob.Id)).Select(m =>
                             new SAction
                             {
                                 ActionName = "Open " + m,
                                 ActionToInvoke = () => { EnvironmentExtensions.OpenBrowser(m); }
                             })
                         .ToList();
                     launchActionsWindow.AddActions(actions);
                     launchActionsWindow.Show();
                 }));
                }
            }
            if (canProvideMemory)
            {
                HandleMemoryLogic(((ILogicImportable)mw.Logic).PluginCodeInformation);
            }
            foreach (var item in ((ILogicImportable)mw.Logic).PluginCodeInformation)
            {
                HandleCommandPalette(item);
            }

            ComandPalette.AddItem(new CommandPaletteItem("Increase pattern", "increase pattern", (x) =>
         {
             if (mw.Player is ISelector s)
             {
                 var c = s.CurrentPattern;
                 if (c + 1 < s.NumberOfPatterns)
                 {
                     s.SetPattern(c + 1);
                 }
             }
         }));

            ComandPalette.AddItem(new CommandPaletteItem("Decrease pattern", "decrease pattern", (x) =>
         {
             if (mw.Player is ISelector s)
             {
                 var c = s.CurrentPattern;
                 if (c -1>0)
                 {
                     s.SetPattern(c - 1);
                 }
             }
         }));

            mw.Logic.Log = logger;
            mw.Logic.ProcessFiles(desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
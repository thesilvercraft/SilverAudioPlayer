using System.Collections;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;

public class BootupLogic
{
    public void ConfigureConfigPath()
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
    }
    public void ConfigureHttpClient()
    {
        SilverAudioPlayer.Shared.HttpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SilverAudioPlayer/8.0 (+https://gitlab.com/silvercraft/SilverAudioPlayer)");
    }
    public void SetupCrashFallNets(Action<Exception> onFatalError)
    {
        AppDomain.CurrentDomain.UnhandledException += (x, y) =>
        {
            var ex = y.ExceptionObject as Exception ?? new Exception($"Unknown CLR Exception object: {y.ExceptionObject}");
            if (y.IsTerminating)
            {
                Log.Fatal(ex,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException, Terminating!!!");
                onFatalError((Exception)y.ExceptionObject);
            }
            else
            {
                Log.Error(ex,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (x, y) =>
        {
            Log.Fatal(y.Exception,
                "Unhandled exception caught by TaskScheduler.UnobservedTaskException, Terminating!!!");
            y.SetObserved();
            onFatalError(y.Exception);
        };
        RxApp.DefaultExceptionHandler = new RxObservableExceptionHandler();
    }

    void HandleCommandPalette<T>(CommandPalette commandPalette, ICodeInformation item, Logic<T> logic, Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs> browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
    {
        switch (item)
        {
            case IAmConfigurable configurable:
                commandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                {
                    showEkorensisWindow.Invoke(configurable);
                }));
                break;
            case IConfigurableUiPlugin configurableUiPlugin:
                commandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                {
                    configurableUiPlugin.ConfigureUi((IConfigurableListener)x);
                }));
                break;
            case IPlayStreamProvider streamProvider:
                commandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                {
                    streamProvider.Use((IPlayStreamProviderListener)x);
                }));
                break;
            case ISyncPlugin syncPlugin:
                commandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                {
                    syncPlugin.Use((ISyncEnvironmentListener)x);
                }));
                break;
        }
        if(item is IExportCommands exporter)
        {
            foreach(var cmd in exporter.GetCommands())
            {
                commandPalette.AddItem(new CommandPaletteItem(cmd.Name, cmd.Id, (x) =>
                {
                    exporter.RaiseCommand(cmd);
                }));
            }
        }

        if (item is IMusicStatusInterface musicStatusInterface)
        {
            commandPalette.AddItem(new CommandPaletteItem("Toggle " + item.Name, "toggle " + item.GetType().Name, (x) =>
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
        if (item is IAmOnceAgainAskingYouForYourMemory memoryAsker && logic.MemoryProvider is IWillTellYouWhereIStoreTheConfigs l)
        {
            commandPalette.AddItem(new CommandPaletteItem("Open config file for " + item.Name, "config file " + item.GetType().Name, (x) =>
            {
                browseConfigLocations.Invoke(item, memoryAsker, l);
            }));
        }
    }
    public void SetupCommandPalette<T>(Logic<T> logic, CommandPalette commandPalette, Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs> browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
    {

        foreach (var item in ((ILogicImportable)logic).PluginCodeInformation)
        {
            HandleCommandPalette(commandPalette, item, logic, browseConfigLocations, showEkorensisWindow);
        }

        commandPalette.AddItem(new CommandPaletteItem("Increase pattern", "increase pattern", (x) =>
        {
            if (logic.Player is not ISelector s) return;
            var c = s.CurrentPattern;
            if (c + 1 < s.NumberOfPatterns)
            {
                s.SetPattern(c + 1);
            }
        }));

        commandPalette.AddItem(new CommandPaletteItem("Decrease pattern", "decrease pattern", (x) =>
        {
            if (logic.Player is not ISelector s) return;
            var c = s.CurrentPattern;
            if (c - 1 > 0) // TODO: CHECK IF PATTERN 0 EXISTS
            {
                s.SetPattern(c - 1);
            }
        }));

    }
    static void HandleMemoryLogic(IEnumerable objectsOwnedByLogic, IWillProvideMemory memoryProvider)
    {
        foreach (var o in objectsOwnedByLogic)
        {
            if (o is IAmOnceAgainAskingYouForYourMemory asker)
            {
                memoryProvider?.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
            }
        }
    }
    public void SetupMemoryLogic<T>(Logic<T> logic) where T : PlayerContext
    {
        if (logic.MemoryProvider is not null)
        {
            HandleMemoryLogic(((ILogicImportable)logic).PluginCodeInformation, logic.MemoryProvider);
        }
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
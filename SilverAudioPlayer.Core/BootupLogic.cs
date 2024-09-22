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
        SilverAudioPlayer.Shared.HttpClient.Client.DefaultRequestHeaders.Add("User-Agent", "SilverAudioPlayer/6.0 (+https://gitlab.com/silvercraft/SilverAudioPlayer)");
    }
    public void SetupCrashFallNets(Action<Exception> onFatalError)
    {
        AppDomain.CurrentDomain.UnhandledException += (x, y) =>
        {
            if (y.IsTerminating)
            {
                Log.Fatal((Exception)y.ExceptionObject,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException, Terminating!!!");
                onFatalError((Exception)y.ExceptionObject);
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
            onFatalError(y.Exception);
        };
        RxApp.DefaultExceptionHandler = new RxObservableExceptionHandler();
    }

    void HandleCommandPalette<T>(ComandPalette comandPalette, ICodeInformation item, Logic<T> logic, Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs> browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
    {
        switch (item)
        {
            case IAmConfigurable configurable:
                comandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                {
                    showEkorensisWindow.Invoke(configurable);
                }));
                break;
            case IConfigurableUiPlugin configurableUiPlugin:
                comandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name, "configure " + item.GetType().Name, (x) =>
                {
                    configurableUiPlugin.ConfigureUi((IConfigurableListener)x);
                }));
                break;
            case IPlayStreamProvider streamProvider:
                comandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                {
                    streamProvider.Use((IPlayStreamProviderListener)x);
                }));
                break;
            case ISyncPlugin syncPlugin:
                comandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name, (x) =>
                {
                    syncPlugin.Use((ISyncEnvironmentListener)x);
                }));
                break;
        }

        if (item is IMusicStatusInterface musicStatusInterface)
        {
            comandPalette.AddItem(new CommandPaletteItem("Toggle " + item.Name, "toggle " + item.GetType().Name, (x) =>
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
            comandPalette.AddItem(new CommandPaletteItem("Open config file for " + item.Name, "config file " + item.GetType().Name, (x) =>
            {
                browseConfigLocations.Invoke(item, memoryAsker, l);
            }));
        }
    }
    public void SetupCommandPalette<T>(Logic<T> logic, ComandPalette commandPalette, Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs> browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
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
            if (c - 1 > 0)
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
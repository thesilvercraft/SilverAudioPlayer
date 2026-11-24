using System.Collections;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;
using Microsoft.Extensions.DependencyInjection;

public class ConfigPathProvider(string basePath) : IConfigPathProvider
{
    public string BasePath { get; set; } = basePath;

    public string GetPath(string subpath)
    {
        return Path.Combine(BasePath, subpath);
    }
}

public class BootupLogic
{
    public IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        IConfiguration _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        services.AddSingleton(_configuration);
        services.AddSingleton<IConfigPathProvider>(new ConfigPathProvider(GetConfigPath()));
        var userAgent = _configuration["HttpClientDetails"] ??
                        "SilverAudioPlayer/8.0 (+https://gitlab.com/silvercraft/SilverAudioPlayer)";
        services.AddHttpClient(_configuration["HttpClientName"] ?? "SilverAudioPlayer",
            x => x.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));
        services.AddHttpClient("",
            x => x.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));
        services.AddSingleton(typeof(ISharedPool<>), typeof(SharedPool<>));
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(_configuration)
            .WriteTo.Console()
            .CreateLogger();
        services.AddSingleton(Log.Logger);
        services.AddSingleton<IMediator>(new Mediator());
        services.AddLogging(builder => builder.AddSerilog());
        return services.BuildServiceProvider();
    }

    string GetConfigPath()
    {
        var confdir = Path.Combine(AppContext.BaseDirectory, "Configs");
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, ".portable")))
        {
            if (OperatingSystem.IsLinux())
            {
                var configBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(configBase))
                {
                    confdir = Path.Combine(configBase, "SilverAudioPlayer");
                }
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

        return confdir;
    }


    public void SetupCrashFallNets(Action<Exception> onFatalError)
    {
        AppDomain.CurrentDomain.UnhandledException += (x, y) =>
        {
            var ex = y.ExceptionObject as Exception ??
                     new Exception($"Unknown CLR Exception object: {y.ExceptionObject}");
            if (y.IsTerminating)
            {
                Log.Fatal(ex,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException, Terminating!!!");
                onFatalError(ex);
            }
            else
            {
                Log.Error(ex,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (x, y) =>
        {
            Log.Error(y.Exception,
                "Unhandled exception caught by TaskScheduler.UnobservedTaskException");
            y.SetObserved();
        };
        RxApp.DefaultExceptionHandler = new RxObservableExceptionHandler();
    }

    void HandleCommandPalette<T>(CommandPalette commandPalette, ICodeInformation item, Logic<T> logic,
        Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs>
            browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
    {
        switch (item)
        {
            case IAmConfigurable configurable:
                commandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name,
                    "configure " + item.GetType().Name, (x) => { showEkorensisWindow.Invoke(configurable); }));
                break;
            case IConfigurableUiPlugin configurableUiPlugin:
                commandPalette.AddItem(new CommandPaletteItem("Configure " + item.Name,
                    "configure " + item.GetType().Name,
                    (x) =>
                    {
                        if (x is IConfigurableListener listener) configurableUiPlugin.ConfigureUi(listener);
                    }));
                break;
            case IPlayStreamProvider streamProvider:
                commandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name,
                    (x) =>
                    {
                        if (x is IPlayStreamProviderListener listener) streamProvider.Use(listener);
                    }));
                break;
            case ISyncPlugin syncPlugin:
                commandPalette.AddItem(new CommandPaletteItem("Use " + item.Name, "use " + item.GetType().Name,
                    (x) =>
                    {
                        if (x is ISyncEnvironmentListener listener) syncPlugin.Use(listener);
                    }));
                break;
        }

        if (item is IExportCommands exporter)
        {
            foreach (var cmd in exporter.GetCommands())
            {
                commandPalette.AddItem(new CommandPaletteItem(cmd.Name, cmd.Id,
                    (x) => { exporter.RaiseCommand(cmd); }));
            }
        }

        if (item is IMusicStatusInterface musicStatusInterface)
        {
            commandPalette.AddItem(new CommandPaletteItem("Toggle " + item.Name, "toggle " + item.GetType().Name, (x) =>
            {
                if (x is not IMusicStatusInterfaceListener listener) return;
                if (musicStatusInterface.IsStarted)
                {
                    musicStatusInterface.StopIPC(listener);
                }
                else
                {
                    musicStatusInterface.StartIPC(listener);
                }
            }));
        }

        if (item is IAmOnceAgainAskingYouForYourMemory memoryAsker &&
            logic.MemoryProvider is IWillTellYouWhereIStoreTheConfigs l)
        {
            commandPalette.AddItem(new CommandPaletteItem("Open config file for " + item.Name,
                "config file " + item.GetType().Name, (x) => { browseConfigLocations.Invoke(item, memoryAsker, l); }));
        }
    }

    public void SetupCommandPalette<T>(Logic<T> logic, CommandPalette commandPalette,
        Action<ICodeInformation, IAmOnceAgainAskingYouForYourMemory, IWillTellYouWhereIStoreTheConfigs>
            browseConfigLocations, Action<IAmConfigurable> showEkorensisWindow) where T : PlayerContext
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
            if (c - 1 >= 0)
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
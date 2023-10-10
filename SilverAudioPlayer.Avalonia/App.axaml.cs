using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Silver.Composition;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
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

    protected ProvidersReturnedNullException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
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

        var render = logEvent.RenderMessage()+'\n';
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
    public  static string _configPath;

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

            if (!File.Exists(_configPath)) mwconfreader.Write(new Config(), _configPath);
            mwconfig = mwconfreader.Read(_configPath) ?? new Config();
            if (mwconfig.UserLogPreferences==ConfigStyle.Unknown)
            {
                //todo: show user dialog to choose
            }
            //todo: logging based on config
            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink, LogEventLevel.Debug)
                .WriteTo.Debug(LogEventLevel.Debug)
                .CreateLogger();
                //LogWindow lw = new();
            //lw.Show();
            Log.Logger = logger;
            logger.Warning("Test warning");
            AppDomain.CurrentDomain.UnhandledException += (x, y) =>
            {
                Log.Error((Exception)y.ExceptionObject,
                    "Unhandled exception caught by AppDomain.CurrentDomain.UnhandledException, Terminating: {IsTerminating}",
                    y.IsTerminating);
            };
            TaskScheduler.UnobservedTaskException += (x,y) =>
            {
                Log.Error(y.Exception,
                    "Unhandled exception caught by TaskScheduler.UnobservedTaskException");
            };
            RxApp.DefaultExceptionHandler = new RxObservableExceptionHandler();
            Logger.GetLoggerFunc += e => logger.ForContext(e);
        
            var mw = new MainWindow();
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            desktop.MainWindow = mw;
            PlatformLogicHelper.SatisfyImports(mw.Logic);
            if (mw.Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            mw.Logic.PlayableMimes = new List<MimeType>();
            bool canProvideMemory = mw.Logic.MemoryProvider != null;
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

            if (canProvideMemory)
            {
                HandleMemoryLogic(mw.Logic.MetadataProviders);
                HandleMemoryLogic(mw.Logic.MusicStatusInterfaces);
                HandleMemoryLogic(mw.Logic.WakeLockInterfaces);
                HandleMemoryLogic(mw.Logic.SyncPlugins);
                HandleMemoryLogic(mw.Logic.PlaylistReaders);
            }

            mw.Logic.Log = logger;
            mw.Logic.ProcessFiles(desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
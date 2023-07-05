using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;
using SilverMagicBytes;

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

public class App : Application
{
    private CompositionHost Container;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var confdir = Path.Combine(AppContext.BaseDirectory, "Configs");
            if (OperatingSystem.IsLinux())
            {
                confdir = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), "SilverAudioPlayer");
            }
            if (!Directory.Exists(confdir))
            {
                Directory.CreateDirectory(confdir);
            }
            ConfigPath.Set(confdir);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(confdir)
                .AddJsonFile(Path.Combine(confdir, "appsettings.json"), true)
                .Build();
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)

                .WriteTo.Debug()
                .CreateLogger();
            Log.Logger = logger;

            AppDomain.CurrentDomain.UnhandledException += (x, y) =>
            {
                Log.Error((Exception)y.ExceptionObject, "Unhandled exception caught in AppDomain.CurrentDomain.UnhandledException, Terminating: {IsTerminating}", y.IsTerminating);
            };
            Logger.GetLoggerFunc += e => logger.ForContext(e);

            var mw = new MainWindow();
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            desktop.MainWindow = mw;
           

            List<Assembly> assemblies = new();
            PlatformLogicHelper.LoadAssemblies(ref assemblies);
            var catalog = new ContainerConfiguration();
            catalog.WithAssemblies(assemblies);
            Container = catalog.CreateContainer();
            Container.SatisfyImports(mw.Logic);
            if (mw.Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            mw.Logic.PlayableMimes = new List<MimeType>();
            bool canProvideMemory = mw.Logic.MemoryProvider != null;
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
            foreach (var provider in mw.Logic.MetadataProviders)
            {
                if(canProvideMemory && provider is IAmOnceAgainAskingYouForYourMemory asker)
                {
                    mw.Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                }
            }
            foreach (var provider in mw.Logic.MusicStatusInterfaces)
            {
                if (canProvideMemory && provider is IAmOnceAgainAskingYouForYourMemory asker)
                {
                    mw.Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                }
            }
            foreach (var provider in mw.Logic.WakeLockInterfaces)
            {
                if (canProvideMemory && provider is IAmOnceAgainAskingYouForYourMemory asker)
                {
                    mw.Logic.MemoryProvider.RegisterObjectsToRemember(asker.ObjectsToRememberForMe);
                }
            }
            mw.Logic.log = logger;
            mw.Logic.ProcessFiles(desktop.Args);
        }
        base.OnFrameworkInitializationCompleted();
    }

    
}
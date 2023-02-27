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

    [SupportedOSPlatform("windows10.1709")]
    public static bool GetThemePreference(bool fallback = false)
    {
        var reg = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        if (reg != null) return reg.GetValue("AppsUseLightTheme") is int theme && theme == 0;
        return fallback;
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var confdir = Path.Combine(AppContext.BaseDirectory, "Configs");
            if (!Directory.Exists(confdir))
            {
                Directory.CreateDirectory(confdir);
            }
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
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
            if (WindowExtensions.envBackend.GetBool("DisableSAPTransparency") == true)
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 1709))
                    ChangeTheme(GetThemePreference(true));
                else
                    ChangeTheme(WindowExtensions.envBackend.GetByte("LIGHTTHEME") != 1);
            }

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

    private static void ChangeTheme(bool dark)
    {
        Current.Styles.Add(new FluentTheme());
    }
}
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    [Serializable]
    public class ProvidersReturnedNullException : Exception
    {
        public ProvidersReturnedNullException()
        { }

        public ProvidersReturnedNullException(string message) : base(message)
        {
        }

        public ProvidersReturnedNullException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ProvidersReturnedNullException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public partial class App : Application
    {
        private CompositionHost Container;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        [SupportedOSPlatform("windows10.1709")]
        public static bool GetThemePreference(bool fallback = false)
        {
            var reg = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (reg != null)
            {
                return reg.GetValue("AppsUseLightTheme") is int theme && theme == 0;
            }
            return fallback;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mw = new MainWindow();
                Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
                desktop.MainWindow = mw;
                if (WindowExtensions.GetEnv("DisableSAPTransparency") == "true")
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 10)
                    {
                        ChangeTheme(GetThemePreference(true));
                    }
                    else
                    {
                        ChangeTheme(WindowExtensions.GetEnv("LIGHTTHEME") != "1");
                    }
                }
                List<Assembly> assemblies = new();
                // An aggregate catalog that combines multiple catalogs.
                var catalog = new ContainerConfiguration();
                // Adds all the parts found in the same assembly as the Program class.
                void AddAssembliesFrom(string path, string filter)
                {
                    assemblies.AddRange(Directory.GetFiles(path, filter).Select(path2 => AssemblyLoadContext.Default.LoadFromAssemblyPath(path2)).Where(x => x != null));
                }
                void PlatformLogic(string path)
                {
                    switch (Environment.OSVersion.Platform)
                    {
                        case PlatformID.Win32NT:
                            AddAssembliesFrom(path, "SilverAudioPlayer.Windows.*.dll");
                            break;

                        case PlatformID.Xbox:
                            AddAssembliesFrom(path, "SilverAudioPlayer.Xbox360.*.dll");
                            break;
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        AddAssembliesFrom(path, "SilverAudioPlayer.Sheep.*.dll");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        AddAssembliesFrom(path, "SilverAudioPlayer.Unix.*.dll");
                    }
                    AddAssembliesFrom(path, "SilverAudioPlayer.Any.*.dll");
                }
                var nextpath = Path.Combine(AppContext.BaseDirectory, "Extensions");
                if (Directory.Exists(nextpath))
                {
                    PlatformLogic(nextpath);
                }
                PlatformLogic(AppContext.BaseDirectory);
                catalog.WithAssemblies(assemblies);
                Container = catalog.CreateContainer();
                Container.SatisfyImports(mw.Logic);
                if (mw.Logic.PlayProviders == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
                }
                foreach (var provider in mw.Logic.PlayProviders)
                {
                    var name = provider.GetType().Name;
                    Debug.WriteLine($"Play provider {name} loaded.");
                }
                Task.Run(async () =>
                {
                    foreach (var playprovider in mw.Logic.PlayProviders)
                    {
                        if (playprovider != null)
                        {
                            await playprovider.OnStartup();
                        }
                    }
                });
                if (mw.Logic.MetadataProviders == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.MetadataProviders' returned null.");
                }
                foreach (var provider in mw.Logic.MetadataProviders)
                {
                    var name = provider.GetType().Name;
                    Debug.WriteLine($"Metadata provider {name} loaded.");
                }
                if (mw.Logic.MusicStatusInterfaces == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.MusicStatusInterfaces' returned null.");
                }
                if (mw.Logic.WakeLockInterfaces == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.WakeLockInterfaces' returned null.");
                }
                foreach (var provider in mw.Logic.MusicStatusInterfaces)
                {
                    var name = provider.GetType().Name;
                    Debug.WriteLine($"Music status interface {name} loaded.");
                }
                var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), true)
                .Build();
                var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.Debug(Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger();
                Shared.Logger.GetLoggerFunc += (e) => logger.ForContext(e);
                mw.Logic.log = logger;
                mw.ProcessFiles(desktop.Args);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ChangeTheme(bool dark) => Current.Styles.Add(new FluentTheme(new Uri("avares://Avalonia.Themes.Default/DefaultTheme.xaml")) { Mode = dark ? FluentThemeMode.Dark : FluentThemeMode.Light });
    }
}
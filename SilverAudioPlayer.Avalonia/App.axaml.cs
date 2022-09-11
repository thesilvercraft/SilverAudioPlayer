using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private AggregateCatalog Catalog;
        private CompositionContainer Container;

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
                Catalog = new();
                try
                {
                    // An aggregate catalog that combines multiple catalogs.
                    var catalog = new AggregateCatalog();
                    // Adds all the parts found in the same assembly as the Program class.
                    catalog.Catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));
                    if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Extensions")))
                    {
                        catalog.Catalogs.Add(new DirectoryCatalog(Path.Combine(AppContext.BaseDirectory, "Extensions")));
                    }
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Any.*.dll"));
                    switch (Environment.OSVersion.Platform)
                    {
                        case PlatformID.Win32NT:
                            catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Windows.*.dll"));
                            break;

                        case PlatformID.Xbox:
                            catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Xbox360.*.dll"));
                            break;
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Sheep.*.dll"));
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory, "SilverAudioPlayer.Unix.*.dll"));
                    }
                    // Create the CompositionContainer with the parts in the catalog.
                    Container = new CompositionContainer(catalog);
                    Container.ComposeParts(this);
                }
                catch (CompositionException compositionException)
                {
                    Console.WriteLine(compositionException.ToString());
                }
                Container.SatisfyImportsOnce(mw.Logic);
                if (mw.Logic.PlayProviders == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
                }
                foreach (var provider in mw.Logic.PlayProviders)
                {
                    var name = provider.Value.GetType().Name;
                    Debug.WriteLine($"Play provider {name} loaded.");
                }
                Task.Run(async () =>
                {
                    foreach (var playprovider in mw.Logic.PlayProviders)
                    {
                        if (playprovider?.Value != null)
                        {
                            await playprovider.Value.OnStartup();
                        }
                    }
                });
                if (mw.Logic.MetadataProviders == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.MetadataProviders' returned null.");
                }
                foreach (var provider in mw.Logic.MetadataProviders)
                {
                    var name = provider.Value.GetType().Name;
                    Debug.WriteLine($"Metadata provider {name} loaded.");
                }
                if (mw.Logic.MusicStatusInterfaces == null)
                {
                    throw new ProvidersReturnedNullException("The 'mw.Logic.MusicStatusInterfaces' returned null.");
                }
                foreach (var provider in mw.Logic.MusicStatusInterfaces)
                {
                    var name = provider.Value.GetType().Name;
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
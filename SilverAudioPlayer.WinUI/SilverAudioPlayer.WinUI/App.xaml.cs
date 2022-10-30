using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Serilog.Events;
using Serilog;
using SilverAudioPlayer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using SilverAudioPlayer.Shared;
using Microsoft.Extensions.Configuration;
using Path = System.IO.Path;
using SilverMagicBytes;
using System.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Serialization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SilverAudioPlayer.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }
        private CompositionHost Container;
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
          
            var logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "log.txt"), LogEventLevel.Information,
                    rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
                .WriteTo.Debug()
                .CreateLogger();
            Logger.GetLoggerFunc += e => logger.ForContext(e);
            Log.Logger = logger;
            var mw= new MainWindow(); 
            m_window = mw;
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            

            List<Assembly> assemblies = new();
            PlatformLogicHelper.LoadAssemblies(ref assemblies);
            var catalog = new ContainerConfiguration();
            catalog.WithAssemblies(assemblies);
            Container = catalog.CreateContainer();
            Container.SatisfyImports(mw.Logic);
            if (mw.Logic.PlayProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.Providers' returned null.");
            mw.Logic.PlayableMimes = new List<MimeType>();
            foreach (var provider in mw.Logic.PlayProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Play provider {name} loaded.");
                if (provider.SupportedMimes != null) mw.Logic.PlayableMimes.AddRange(provider.SupportedMimes);
            }

            var playproviderloadtask = Task.Run(async () =>
            {
                foreach (var playprovider in mw.Logic.PlayProviders)
                    if (playprovider != null)
                        await playprovider.OnStartup();
            });
            if (mw.Logic.MetadataProviders == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.MetadataProviders' returned null.");
            foreach (var provider in mw.Logic.MetadataProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Metadata provider {name} loaded.");
            }

            if (mw.Logic.MusicStatusInterfaces == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.MusicStatusInterfaces' returned null.");
            if (mw.Logic.WakeLockInterfaces == null)
                throw new ProvidersReturnedNullException("The 'mw.Logic.WakeLockInterfaces' returned null.");
            foreach (var provider in mw.Logic.MusicStatusInterfaces)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine($"Music status interface {name} loaded.");
            }

            mw.Logic.log = logger;
           // mw.Logic.ProcessFiles(args.Arguments.Split(" "));
            mw.Activate();
        }

        private Window m_window;
    }
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

}

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;

namespace SilverAudioPlayer
{
    internal class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string?[]? args)
        {
            App a = new();
            a.Run(args);
        }
    }

    public class App
    {
        private CompositionContainer _container;

        public App()
        {
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
                else
                {
                    catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory));
                }

                // Create the CompositionContainer with the parts in the catalog.
                _container = new CompositionContainer(catalog);
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Console.WriteLine(compositionException.ToString());
            }
        }

        [STAThread]
        public void Run(string?[]? args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            if (args != null && args.Any(x => string.IsNullOrEmpty(x)))
            {
            }
            var frm1 = new Form1();
            _container.SatisfyImportsOnce(frm1.Logic);
            //ACCESS THE DANG THINGS HERE FOR IT TO WORK
            foreach (var provider in frm1.Logic.Providers)
            {
                Debug.WriteLine(provider.Value.GetType().Name);
            }
            foreach (var provider in frm1.Logic.MetadataProviders)
            {
                Debug.WriteLine(provider.Value.GetType().Name);
            }
            Application.Run(frm1);
        }
    }
}
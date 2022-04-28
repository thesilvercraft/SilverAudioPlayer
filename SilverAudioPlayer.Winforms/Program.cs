using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using SilverAudioPlayer.Winforms;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;   //GuidAttribute

namespace SilverAudioPlayer
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
        private static readonly Mutex mutex = new(true, "8501d027-384f-4a8f-9cf0-4c35936360fa");

        private CompositionContainer _container;

        private void DoContainerShit()
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

                catalog.Catalogs.Add(new DirectoryCatalog(AppContext.BaseDirectory));

                // Create the CompositionContainer with the parts in the catalog.
                _container = new CompositionContainer(catalog);
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Console.WriteLine(compositionException.ToString());
            }
            _container.SatisfyImportsOnce(frm1.Logic);
            //ACCESS THE DANG THINGS HERE FOR IT TO WORK
            if (frm1.Logic.PlayProviders == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.Providers' returned null.");
            }

            foreach (var provider in frm1.Logic.PlayProviders)
            {
                var name = provider.Value.GetType().Name;
                Debug.WriteLine(name);
            }
            if (frm1.Logic.MetadataProviders == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.MetadataProviders' returned null.");
            }
            foreach (var provider in frm1.Logic.MetadataProviders)
            {
                var name = provider.Value.GetType().Name;
                Debug.WriteLine(name);
            }
            if (frm1.Logic.MusicStatusInterfaces == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.MusicStatusInterfaces' returned null.");
            }
            foreach (var provider in frm1.Logic.MusicStatusInterfaces)
            {
                var name = provider.Value.GetType().Name;
                Debug.WriteLine(name);
            }
        }

        private Form1 frm1;

        [STAThread]
        public void Run(string?[]? args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.ThreadException += (sender, args) =>
            {
                Crashes.TrackError(args.Exception);
            };
            Crashes.ShouldAwaitUserConfirmation = () =>
            {
                Thread nt = new(() =>
                {
                    UserCONSENT uc = new();
                    uc.ShowDialog();
                });
                nt.Start();
                return true;
            };
            ApplicationConfiguration.Initialize();
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Debug.WriteLine("AAA");
                frm1 = new Form1();
                DoContainerShit();
                bool ms = true;
                if (args != null && !args.Any(x => string.IsNullOrEmpty(x)))
                {
                    if (args.Contains("--noms"))
                    {
                        args = args.Where(val => val != "--noms").ToArray();
                        ms = false;
                    }
                    if (args.Length == 1 && args[0] == "--reg")
                    {
                        frm1.RegisterInReg();
                    }
                    else
                    {
                        frm1.ProcessFiles(true, args!);
                    }
                }
                if (ms)
                {
                    AppCenter.Start("e18445b9-ac9c-4d5a-af60-318e0cba754b",
                  typeof(Analytics), typeof(Crashes));
                }
                Application.Run(frm1);
                mutex.ReleaseMutex();
            }
            else if (args != null)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "args.txt");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                using StreamWriter w = new(path);
                w.WriteAsync(string.Join('\n', args));
                w.Close();
                w.Dispose();
#if DEBUG
                Process.Start("notepad.exe", path);
#endif
                // send our Win32 message to make the currently running instance
                // jump on top of all the other windows
                NativeMethods.PostMessage(
                    (IntPtr)NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SHOWME,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
        }
    }

    internal static class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_AUDIOPLAYERZ");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);
    }
}
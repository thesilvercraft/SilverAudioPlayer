#if MS

using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Silver.Serilog.MSAppCenterSink;

#endif


using Microsoft.Extensions.Configuration;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Winforms;
using System.Diagnostics;
using System.Runtime.InteropServices;   //GuidAttribute
using System.Security.Principal;
using Microsoft.Win32;
using System.Text;
using System.ComponentModel;
using System.Composition.Hosting;
using System.Reflection;
using System.Runtime.Loader;
using System.Composition;
using SilverAudioPlayer.Core;

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
        private static bool IsElevated
        {
            get
            {
                return WindowsIdentity.GetCurrent().Owner
                  .IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            }
        }

        private static readonly Mutex mutex = new(true, "8501d027-384f-4a8f-9cf0-4c35936360fa");
        private CompositionHost Container;

        private void DoContainerShit()
        {
            List<Assembly> assemblies = new();
            PlatformLogicHelper.LoadAssemblies(ref assemblies);
            var catalog = new ContainerConfiguration();
            catalog.WithAssemblies(assemblies);
            Container = catalog.CreateContainer();
            Container.SatisfyImports(frm1.Logic);
            //ACCESS THE DANG THINGS HERE FOR IT TO WORK
            if (frm1.Logic.PlayProviders == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.Providers' returned null.");
            }

            foreach (var provider in frm1.Logic.PlayProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine(name);
            }
            Task.Run(async () =>
            {
                foreach (var playprovider in frm1.Logic.PlayProviders)
                {
                        await playprovider?.OnStartup();
                }
            });
            if (frm1.Logic.MetadataProviders == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.MetadataProviders' returned null.");
            }
            foreach (var provider in frm1.Logic.MetadataProviders)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine(name);
            }
            if (frm1.Logic.MusicStatusInterfaces == null)
            {
                throw new ProvidersReturnedNullException("The 'frm1.Logic.MusicStatusInterfaces' returned null.");
            }
            foreach (var provider in frm1.Logic.MusicStatusInterfaces)
            {
                var name = provider.GetType().Name;
                Debug.WriteLine(name);
            }
        }

        public static bool ShortCuts()
        {
            bool isBuiltInAdmin = IsElevated;
            if (!isBuiltInAdmin)
            {
                Process proc = new();
                proc.StartInfo.Arguments = "--shortcuts";
                proc.StartInfo.FileName = Application.ExecutablePath;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.Start();
                return true;
            }
            var wsh = new IWshRuntimeLibrary.WshShell();
            IWshRuntimeLibrary.IWshShortcut shortcut = wsh.CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\SendTo\\SilverAudioPlayer (Play).lnk")) as IWshRuntimeLibrary.IWshShortcut;
            shortcut.TargetPath = Application.ExecutablePath;
            shortcut.Save();
            return false;
        }

        public static bool RemoveShortCuts()
        {
            bool isBuiltInAdmin = IsElevated;
            if (!isBuiltInAdmin)
            {
                Process proc = new();
                proc.StartInfo.Arguments = "--rshortcuts";
                proc.StartInfo.FileName = Application.ExecutablePath;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.Start();
                return true;
            }
            var f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\SendTo\\SilverAudioPlayer (Play).lnk");
            if (File.Exists(f))
            {
                File.Delete(f);
            }
            return false;
        }

        [DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private static readonly string[] AssociatedFileTypes = new[] { ".mp3", ".aif", ".aiff", ".flac", ".wav", ".ogg", ".midi", ".mid" };

        public static void RegisterInReg()
        {
            if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayer", string.Empty, string.Empty)))
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer", "", "Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer", "FriendlyTypeName", "AudioPlayerZ Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer\\shell\\open\\command", "",
                    $"{Environment.ProcessPath} \"%1\"");
                foreach (string? type in AssociatedFileTypes)
                {
                    string? a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                    string? val = (string?)Registry.GetValue(a, "", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        StringBuilder name = new("SAP.BAK");
                        string? val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                        while (!string.IsNullOrEmpty(val2))
                        {
                            name.Append(".BAK");
                            val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                        }
                        Registry.SetValue(a, name.ToString(), val);
                    }
                    Registry.SetValue(a, "", "SilverAudioPlayer");
                }
                //this call notifies Windows that it needs to redo the file associations and icons
                _ = SHChangeNotify(0x08000000, 0x2000, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static void DeleteRegistryFolder(RegistryHive registryHive, string fullPathKeyToDelete)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default))
            {
                baseKey.DeleteSubKeyTree(fullPathKeyToDelete);
            }
        }

        public static void RemoveFromReg()
        {
            if (!string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayer", string.Empty, string.Empty)))
            {
                DeleteRegistryFolder(RegistryHive.ClassesRoot, "SilverAudioPlayer");
                foreach (string? type in AssociatedFileTypes)
                {
                    string? a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                    string? val = (string?)Registry.GetValue(a, "", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        string? val2 = (string?)Registry.GetValue(a, "SAP.BAK", "");
                        if (!string.IsNullOrEmpty(val2))
                        {
                            Registry.SetValue(a, "", val2);
                        }
                    }
                }
                //this call notifies Windows that it needs to redo the file associations and icons
                _ = SHChangeNotify(0x08000000, 0x2000, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private Form1 frm1;

        [STAThread]
        public void Run(string?[]? args)
        {
            Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "settings\\")))
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "settings\\"));
            }
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "preferences.xml")))
            {
                File.Move(Path.Combine(AppContext.BaseDirectory, "preferences.xml"), Path.Combine(AppContext.BaseDirectory, "settings\\", "silveraudioplayer.winforms.preferences.xml"));
            }
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), true)
            .Build();
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.Debug(Serilog.Events.LogEventLevel.Verbose)
#if MS
                .WriteTo.MSAppCenter()
#endif
                .CreateLogger();
            Logger.GetLoggerFunc += (e) => logger.ForContext(e);
#if MS

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
#endif
            AutoUpdate au = new("https://silverwebsiterepo.pages.dev/silveraudioplayer/releases", "SilverAudioPlayer", AppContext.BaseDirectory);
            au.OnRun();
            ApplicationConfiguration.Initialize();
            if (mutex.WaitOne(TimeSpan.FromSeconds(2), true))
            {
                Task.Run(async () => await au.CheckForUpdates()).Wait();
                Debug.WriteLine("AAA");
                frm1 = new Form1();
                frm1.Logic.log = logger;
                Log.Logger = logger;

                DoContainerShit();
                if (args?.Any(x => string.IsNullOrEmpty(x)) == false)
                {
                    if (args.Length == 1 && args[0] == "--reg")
                    {
                        RegisterInReg();
                        return;
                    }
                    else if (args.Length == 1 && args[0] == "--removereg")
                    {
                        RemoveFromReg();
                        return;
                    }
                    else if (args.Length == 1 && args[0] == "--shortcuts")
                    {
                        ShortCuts();
                        return;
                    }
                    else if (args.Length == 1 && args[0] == "--rshortcuts")
                    {
                        RemoveShortCuts();
                        return;
                    }
                    else
                    {
                        frm1.Logic.ProcessFiles(args!);
                    }
                }
#if MS
                AppCenter.Start("e18445b9-ac9c-4d5a-af60-318e0cba754b",
                  typeof(Analytics), typeof(Crashes));
#endif
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
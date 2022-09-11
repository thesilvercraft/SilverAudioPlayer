#if SQRL

using Squirrel;

#endif
#if SUP

using Silver.Update;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;

#endif

namespace SilverAudioPlayer.Winforms
{
    internal class AutoUpdate
    {
        private string UpdateUrl;
        private string? AppName;
        private string? RootDir;

        public AutoUpdate(string updateUrl, string? appName, string? rootDir)
        {
            UpdateUrl = updateUrl ?? throw new ArgumentNullException(nameof(updateUrl));
            AppName = appName;
            RootDir = rootDir;
        }

        public void OnRun()
        {
#if SQRL
            SquirrelAwareApp.HandleEvents(
       onInitialInstall: OnAppInstall,
       onAppUninstall: OnAppUninstall,
       onEveryRun: OnAppRun);
#endif
        }

#if SQRL

        private static void OnAppInstall(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
            App.RegisterInReg();
            if (App.ShortCuts())
            {
                Environment.Exit(5);
            }
        }

        private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
        {
            tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
            App.RemoveFromReg();
            if (App.RemoveShortCuts())
            {
                Environment.Exit(-5);
            }
        }

        private static void OnAppRun(SemanticVersion version, IAppTools tools, bool firstRun)
        {
            tools.SetProcessAppUserModelId();
            if (firstRun) MessageBox.Show("Thanks for installing my application!");
        }

#endif

        public async Task CheckForUpdates()
        {
#if SQRL
            using var manager = new UpdateManager(UpdateUrl, AppName, RootDir);
            await manager.UpdateApp();
#endif
#if SUP
            var assembly = typeof(Program).Assembly;
            var attribute = (GuidAttribute)assembly.GetCustomAttributes(typeof(GuidAttribute), true)[0];
            var id = attribute.Value;
            try
            {
                  Updater a = new(UpdateUrl, Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "unknown");
                  Updater.UpdateState? updateav = await a.CheckForUpdates();
                  if (updateav is not null && updateav is not Updater.UpToDate && updateav is not Updater.UpToDateButFilesModified && updateav is Updater.NotUpToDate)
                  {
                      Tuple<string, string>? stuff = await a.ShowUpdateQuestionDialog(AppName);
                      if (stuff != null)
                      {
                          Process.Start(stuff.Item1, stuff.Item2);
                          await Task.Delay(1000);
                          Application.Exit();
                      }
                  }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
#endif
        }
    }
}
#if SQRL

using Squirrel;

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

        }
    }
}
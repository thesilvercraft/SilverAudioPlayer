using Avalonia;
using Avalonia.Dialogs;
using System;
namespace SilverAudioPlayer.Avalonia
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var a = BuildAvaloniaApp();
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                a = a.UseManagedSystemDialogs();
            }
            a.StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
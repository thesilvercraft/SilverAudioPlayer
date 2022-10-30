using System;
using Avalonia;
using Avalonia.Dialogs;

namespace SilverAudioPlayer.Avalonia;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var a = BuildAvaloniaApp();
        if (Environment.OSVersion.Platform != PlatformID.Win32NT && Environment.GetEnvironmentVariable("SAPUseNativeDialog")!="yes") a = a.UseManagedSystemDialogs();
        a.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
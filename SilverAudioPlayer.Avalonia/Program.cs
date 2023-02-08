using System;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Svg.Skia;

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
        if (Environment.GetEnvironmentVariable("SAPUseNativeDialog")=="yes") a = a.UseManagedSystemDialogs();
        a.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(global::Avalonia.Svg.Skia.Svg).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
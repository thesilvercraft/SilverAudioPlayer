using System;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Svg.Skia;
using SilverCraft.AvaloniaUtils;

namespace Catenoid;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var app = BuildAvaloniaApp();
        if(WindowExtensions.envBackend.GetString("SAPTransparency")==null)
        {
            WindowExtensions.envBackend.SetString("SAPTransparency","Mica,AcrylicBlur,Blur,None");
        }
        if (Environment.GetEnvironmentVariable("SAPUseNativeDialog")=="yes") app = app.UseManagedSystemDialogs();
        app.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
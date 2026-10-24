using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Svg.Skia;
using SilverCraft.AvaloniaUtils;

namespace Catenoid;

internal static class Program
{
    public const string KDialogLocation = "/usr/bin/kdialog";
    private static void HandleExceptionCode(Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        if (File.Exists(KDialogLocation))
        {
            Process.Start(KDialogLocation, $"--desktopfile silveraudioplayer.desktop --title \"Error\" --detailederror \"{ex.ToString()}\".\r\n");
        }
    }
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        WindowsAppInitializer.InitializeAppId();
        using (Mutex mutex = new(false, @"Global\" + "Catenoid"))
        {
            var hasMutex = false;
            try
            {
                if (WindowExtensions.envBackend.GetString("SAPIgnoreMutex") == null)
                {
                    var shortLivedHttpClient = new HttpClient();
                    if (!mutex.WaitOne(0, false))
                    {
                        if (args is { Length: > 0 })
                        {
                            try
                            {
                                var r = Task.Run(async () => await shortLivedHttpClient.PostAsync("http://localhost:36169/loadfiles", JsonContent.Create(args))).GetAwaiter().GetResult();
                                r.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                HandleExceptionCode(ex);
                            }
                        }
                        else
                        {
                            try
                            {
                                var r = Task.Run(async () => await shortLivedHttpClient.GetAsync("http://localhost:36169/state")).GetAwaiter().GetResult();
                                r.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                HandleExceptionCode(ex);
                            }
                        }
                        return;
                    }
                }
                hasMutex = true;
                var app = BuildAvaloniaApp();
                if (WindowExtensions.envBackend.GetString("SAPTransparency") == null)
                {
                    WindowExtensions.envBackend.SetString("SAPTransparency", "Mica,AcrylicBlur,Blur,None");
                }
                if (Environment.GetEnvironmentVariable("SAPUseNativeDialog") == "yes") app = app.UseManagedSystemDialogs();
                app.StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                if (hasMutex)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
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

public static class WindowsAppInitializer
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    public static void InitializeAppId()
    {
        SetCurrentProcessExplicitAppUserModelID("SilverCraft.Catenoid");
    }
}
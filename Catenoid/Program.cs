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
using System.Runtime.Versioning;
namespace Catenoid;

internal static class Program
{
    public const string KDialogLocation = "/usr/bin/kdialog";
    private static void HandleExceptionCode(Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        if (File.Exists(KDialogLocation))
        {
            var psi = new ProcessStartInfo
            {
                FileName = KDialogLocation,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("--desktopfile");
            psi.ArgumentList.Add("silveraudioplayer.desktop");
            psi.ArgumentList.Add("--title");
            psi.ArgumentList.Add("Error");
            psi.ArgumentList.Add("--detailederror");
            psi.ArgumentList.Add("An unexpected error occurred.");
            psi.ArgumentList.Add(ex.ToString());
            using var proc = Process.Start(psi);
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
                    bool acquired;
                    try
                    {
                        acquired = mutex.WaitOne(0, false);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        using var shortLivedHttpClient = new HttpClient();
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
                if (Environment.GetEnvironmentVariable("SAPUseNativeDialog") == "yes" &&
                    (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
                {
                    app = app.UseManagedSystemDialogs();
                }
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
        var builder = AppBuilder.Configure<App>();
           // builder = builder.UseWayland().UseSkia().UseHarfBuzz();
            builder = builder.UsePlatformDetect();
#if DEBUG
        return builder.LogToTrace();
#else
        return builder;
#endif
    }
}

public static partial class WindowsAppInitializer
{
    [SupportedOSPlatform("windows")]
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetCurrentProcessExplicitAppUserModelID(string AppID);

    public static void InitializeAppId()
    {
        if (OperatingSystem.IsWindows())
        {
            if(SetCurrentProcessExplicitAppUserModelID("SilverCraft.Catenoid") != 0)
            {
                // TODO: log
            }
        }
    }
}

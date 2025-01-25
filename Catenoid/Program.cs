using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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
        using (Mutex mutex = new(false, @"Global\" + "Catenoid"))
        {
            bool hasMutex = false;
            try
            {
                if (WindowExtensions.envBackend.GetString("SAPIgnoreMutex") == null)
                {
                    if (!mutex.WaitOne(0, false))
                    {
                        if (args is { Length: > 0 })
                        {
                            try
                            {
                                var r = Task.Run(async () => await SilverAudioPlayer.Shared.HttpClient.Client.PostAsync("http://localhost:36169/loadfiles", JsonContent.Create(args))).GetAwaiter().GetResult();
                                r.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.ToString());
                                if (File.Exists("/usr/bin/kdialog"))
                                {
                                    Process.Start("kdialog", $"--desktopfile silveraudioplayer.desktop --title \"Error\" --detailederror \"{ex.ToString()}\".\r\n");
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                var r = Task.Run(async () => await SilverAudioPlayer.Shared.HttpClient.Client.GetAsync("http://localhost:36169/state")).GetAwaiter().GetResult();
                                r.EnsureSuccessStatusCode();

                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.ToString());
                                if (File.Exists("/usr/bin/kdialog"))
                                {
                                    Process.Start("kdialog", $"--desktopfile silveraudioplayer.desktop --title \"Error\" --detailederror \"{ex.ToString()}\".\r\n");
                                }
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

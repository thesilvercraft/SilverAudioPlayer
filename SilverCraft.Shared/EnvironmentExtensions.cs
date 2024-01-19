using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilverCraft.Shared;

public static class EnvironmentExtensions
{
    public static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else
            throw new NotSupportedException("Os not supported");
    }
    public static void OpenFolder(string folder)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("explorer.exe", $"{folder}") { CreateNoWindow = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string method = null;
            if (File.Exists(folder))
            {
                method = "org.freedesktop.FileManager1.ShowItems";
            }
            else if (Directory.Exists(folder))
            {
                method = "org.freedesktop.FileManager1.ShowFolders";
            }
            if(method==null) return;
            Process dbusProcess = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "dbus-send",
                    Arguments = $"--session --print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 {method} array:string:\"{folder}\" string:\"\"",
                    UseShellExecute = true
                }
            };      
            dbusProcess.Start();
        }
        else
            throw new NotSupportedException("Os not supported");
    }

    public static string NewHexString() => $"#{Random.Shared.Next(0x1000000):X6}";
}
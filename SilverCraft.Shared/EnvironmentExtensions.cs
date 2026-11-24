using System.Diagnostics;

namespace SilverCraft.Shared;

public static class EnvironmentExtensions
{
    public static void OpenBrowser(string url)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        else if (OperatingSystem.IsLinux())
            Process.Start("xdg-open", url);
        else
            throw new NotSupportedException("Os not supported");
    }
    public static void OpenFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            if(File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select, \"{path}\"") { CreateNoWindow = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { CreateNoWindow = true });
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var fullPath = Path.GetFullPath(path);
            string? method = null;
            if (File.Exists(fullPath))
            {
                method = "org.freedesktop.FileManager1.ShowItems";
            }
            else if (Directory.Exists(fullPath))
            {
                method = "org.freedesktop.FileManager1.ShowFolders";
            }
            if (method == null) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = "dbus-send",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--session");
            startInfo.ArgumentList.Add("--dest=org.freedesktop.FileManager1");
            startInfo.ArgumentList.Add("--type=method_call");
            startInfo.ArgumentList.Add("/org/freedesktop/FileManager1");
            startInfo.ArgumentList.Add(method);
            startInfo.ArgumentList.Add($"array:string:{fullPath}");
            startInfo.ArgumentList.Add("string:");

            try
            {
                using var dbusProcess = Process.Start(startInfo);
            }
            catch (Exception)
            {
                // Fallback if dbus-send is unavailable inside the container/toolbox
                var targetDir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) ?? fullPath : fullPath;
                using var fallback = Process.Start(new ProcessStartInfo("xdg-open", targetDir) { UseShellExecute = false });
            }
        }
        else
            throw new NotSupportedException("Os not supported");
    }

    public static string NewHexString() => $"#{Random.Shared.Next(0x1000000):X6}";
}
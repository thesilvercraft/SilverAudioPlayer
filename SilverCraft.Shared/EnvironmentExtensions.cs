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
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            throw new NotSupportedException("Os not supported");
    }
    public static string NewHexString() => $"#{Random.Shared.Next(0x1000000):X6}";
}
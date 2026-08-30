using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Catenoid;

public static class RegistryRegistration
{
    private const string WinProgId = "SilverAudioPlayerA";
    private const string DesktopFileName = "Catenoid.desktop";

    public static void RegisterUrlScheme(MainWindow mainWindow)
    {
        if (OperatingSystem.IsWindows())
            RegisterURLSchemeWindows(mainWindow);
        else if (OperatingSystem.IsLinux())
            RegisterURLSchemeLinux(mainWindow);
    }

    public static void UnRegisterUrlScheme(MainWindow mainWindow)
    {
        if (OperatingSystem.IsWindows())
            UnRegisterUrlSchemeWindows(mainWindow);
        else if (OperatingSystem.IsLinux())
            UnRegisterUrlSchemeLinux(mainWindow);
    }

    public static bool IsReg()
    {
        if (OperatingSystem.IsWindows())
        {
            return !string.IsNullOrEmpty((string?)Registry.GetValue(@$"HKEY_CURRENT_USER\Software\Classes\{WinProgId}",
                string.Empty, null));
        }

        if (OperatingSystem.IsLinux())
        {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/applications", DesktopFileName));
        }

        return false;
    }

    [SupportedOSPlatform("linux")]
    public static void RegisterURLSchemeLinux(MainWindow mainWindow)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appsDir = Path.Combine(userProfile, ".local/share/applications");
        var desktopFilePath = Path.Combine(appsDir, DesktopFileName);

        Directory.CreateDirectory(appsDir);

        if (File.Exists(desktopFilePath)) return;
        var allMimes = mainWindow.Logic.SupportedTrackAndPlaylistMimes();
        var playableMimes = allMimes.Item1;

        var desktopEntry = $"""
                            [Desktop Entry]
                            Comment[en_GB]=The SilverCraft Audio Player
                            Comment=The SilverCraft Audio Player
                            Path={AppContext.BaseDirectory}
                            Exec="{Environment.ProcessPath}" %U
                            GenericName[en_GB]=Audio player
                            GenericName=Audio player
                            Icon=catenoid
                            Keywords=Player;Audio;Dotnet;
                            MimeType=x-content/audio-player;{string.Join(';', playableMimes)};
                            Name[en_GB]=Catenoid
                            Name=Catenoid
                            NoDisplay=false
                            StartupNotify=true
                            Terminal=false
                            Type=Application
                            X-KDE-Protocols=http,https
                            Categories=AudioVideo;Player;Midi;Music;Audio;
                            """;

        File.WriteAllText(desktopFilePath, desktopEntry);

        var mimeXmlPath = Path.Combine(AppContext.BaseDirectory, "custom-mime", "silvercraft-spl.xml");
        if (File.Exists(mimeXmlPath))
        {
            RunProcess("xdg-mime", $"install \"{mimeXmlPath}\"");
        }

        var iconsDir = Path.Combine(userProfile, ".local/share/icons");
        Directory.CreateDirectory(iconsDir);
        var localIconPath = Path.Combine(iconsDir, "catenoid.svg");

        using var iconSource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Catenoid.icon.svg");
        if (iconSource == null) return;
        using var iconFile = File.Create(localIconPath);
        iconSource.CopyTo(iconFile);
    }

    [SupportedOSPlatform("linux")]
    private static void UnRegisterUrlSchemeLinux(MainWindow _)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var mimeXmlPath = Path.Combine(AppContext.BaseDirectory, "custom-mime", "silvercraft-spl.xml");

        if (File.Exists(mimeXmlPath))
        {
            RunProcess("xdg-mime", $"uninstall \"{mimeXmlPath}\"");
        }

        var desktopPath = Path.Combine(userProfile, ".local/share/applications", DesktopFileName);
        if (File.Exists(desktopPath))
        {
            File.Delete(desktopPath);
        }

        var iconPath = Path.Combine(userProfile, ".local/share/icons", "catenoid.svg");
        if (File.Exists(iconPath))
        {
            File.Delete(iconPath);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterURLSchemeWindows(MainWindow mainWindow)
    {
        const string baseKeyPath = $@"HKEY_CURRENT_USER\Software\Classes\{WinProgId}";
        if (!string.IsNullOrEmpty((string?)Registry.GetValue(baseKeyPath, string.Empty, null)))
            return;

        Registry.SetValue(baseKeyPath, string.Empty, "Audio File");
        Registry.SetValue(baseKeyPath, "FriendlyTypeName", "Audio File");
        Registry.SetValue($@"{baseKeyPath}\shell\open\command", string.Empty, $"\"{Environment.ProcessPath}\" \"%1\"");

        var extensions = mainWindow.Logic.PlayableMimes
            .Where(x => x.FileExtensions.Length > 0)
            .SelectMany(x => x.FileExtensions);

        foreach (var ext in extensions)
        {
            var extKey = $@"HKEY_CURRENT_USER\Software\Classes\{ext}";
            var currentVal = (string?)Registry.GetValue(extKey, string.Empty, null);

            if (!string.IsNullOrEmpty(currentVal) && currentVal != WinProgId)
            {
                if (Registry.GetValue(extKey, "SAPA.BAK", null) == null)
                {
                    Registry.SetValue(extKey, "SAPA.BAK", currentVal);
                }
            }

            Registry.SetValue(extKey, string.Empty, WinProgId);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void UnRegisterUrlSchemeWindows(MainWindow mainWindow)
    {
        var baseKeyPath = $@"HKEY_CURRENT_USER\Software\Classes\{WinProgId}";
        if (Registry.GetValue(baseKeyPath, string.Empty, null) == null)
            return;

        DeleteRegistryFolder(RegistryHive.CurrentUser, $@"Software\Classes\{WinProgId}");

        var extensions = mainWindow.Logic.PlayableMimes
            .Where(x => x.FileExtensions.Length > 0)
            .SelectMany(x => x.FileExtensions);

        foreach (var ext in extensions)
        {
            var extKey = $@"HKEY_CURRENT_USER\Software\Classes\{ext}";
            var currentVal = (string?)Registry.GetValue(extKey, string.Empty, null);

            if (currentVal != WinProgId) continue;
            var backupVal = (string?)Registry.GetValue(extKey, "SAPA.BAK", null);
            if (!string.IsNullOrEmpty(backupVal))
            {
                Registry.SetValue(extKey, string.Empty, backupVal);
                using var subKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}", true);
                subKey?.DeleteValue("SAPA.BAK", false);
            }
            else
            {
                using var subKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}", true);
                subKey?.DeleteValue(string.Empty, false);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public static void DeleteRegistryFolder(RegistryHive registryHive, string fullPathKeyToDelete)
    {
        using var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default);
        baseKey.DeleteSubKeyTree(fullPathKeyToDelete, throwOnMissingSubKey: false);
    }

    private static void RunProcess(string fileName, string arguments)
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        proc?.WaitForExit();
    }
}
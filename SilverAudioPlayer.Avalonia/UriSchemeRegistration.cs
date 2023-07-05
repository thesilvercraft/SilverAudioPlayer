using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace SilverAudioPlayer.Avalonia;

public static class RegistryRegistration
{
    public static void RegisterUrlScheme(MainWindow mainWindow)
    {
        if (OperatingSystem.IsWindows())
        {
            RegisterURLSchemeWindows(mainWindow);
        }
        else if (OperatingSystem.IsLinux())
        {
            //based and redpilled
            RegisterURLSchemeLinux(mainWindow);
        }
    }
    public static void UnRegisterUrlScheme(MainWindow mainWindow)
    {
        if (OperatingSystem.IsWindows())
        {
            UnRegisterUrlSchemeWindows(mainWindow);
        }
        else if (OperatingSystem.IsLinux())
        {
            //based and redpilled
            UnRegisterUrlSchemeLinux(mainWindow);
        }
    }

    private static void UnRegisterUrlSchemeLinux(MainWindow mainWindow)
    {
         File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/share/applications/silveraudioplayer.desktop"));
         Process.Start("kdesu", $"rm /usr/share/pixmaps/sap.svg");
    }

    public static void DeleteRegistryFolder(RegistryHive registryHive, string fullPathKeyToDelete)
    {
        using var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default);
        baseKey.DeleteSubKeyTree(fullPathKeyToDelete);
    }
    [SupportedOSPlatform("windows")]
    private static void UnRegisterUrlSchemeWindows(MainWindow mainWindow)
    {
        if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                string.Empty))) return;
        DeleteRegistryFolder(RegistryHive.ClassesRoot, "SilverAudioPlayerA");
        foreach (var type in mainWindow.Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                     .SelectMany(x => x.FileExtensions).ToList())
        {
            var a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
            var val = (string?)Registry.GetValue(a, "", "");
            if (string.IsNullOrEmpty(val)) continue;
            var val2 = (string?)Registry.GetValue(a, "SAPA.BAK", "");
            if (!string.IsNullOrEmpty(val2)) Registry.SetValue(a, "", val2);
        }
    }


    public static bool IsReg()
    {
        if (OperatingSystem.IsWindows())
        {
            return !string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                string.Empty));
        }
        else if (OperatingSystem.IsLinux())
        {
            //based and redpilled
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/applications/silveraudioplayer.desktop"));
        }

        return false;
    }
    public static void RegisterURLSchemeLinux(MainWindow mainWindow)
    {
        var f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/share/applications/silveraudioplayer.desktop");
        if (!File.Exists(f))
        {
            var playableMimes = mainWindow.Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                .SelectMany(x => x.AlternativeTypes).ToList();
            playableMimes.AddRange(mainWindow.Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                .Select(x => x.Common));
            File.WriteAllText(f, @$"[Desktop Entry]
[Desktop Entry]
Comment[en_GB]=The SilverCraft Audio Player
Comment=The SilverCraft Audio Player
Path={AppContext.BaseDirectory}
Exec={Environment.ProcessPath} %U
GenericName[en_GB]=Audio player
GenericName=Audio player
Icon=sap
Keywords=Player;Audio;Dotnet;
MimeType=x-content/audio-player;{string.Join(';', playableMimes)};
Name[en_GB]=SilverAudioPlayer
Name=SilverAudioPlayer
NoDisplay=false
StartupNotify=true
Terminal=false
TerminalOptions=
Type=Application
X-KDE-Protocols=http,https
X-KDE-SubstituteUID=false");
            using var iconFile = File.OpenWrite("/tmp/sap.svg");
            using var iconSource = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SilverAudioPlayer.Avalonia.icon.svg");
            iconSource.CopyTo(iconFile);
            Process.Start("kdesu", "mv /tmp/sap.svg /usr/share/pixmaps/sap.svg");
        }
    }

    [SupportedOSPlatform("windows")]
    public static void RegisterURLSchemeWindows(MainWindow mainWindow)
    {
        if (!string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                string.Empty))) return;
        Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA", "", "Audio File");
        Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA", "FriendlyTypeName",
            "Audio File");
        Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA\\shell\\open\\command", "",
            $"{Environment.ProcessPath} \"%1\"");
        foreach (var type in mainWindow.Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                     .SelectMany(x => x.FileExtensions).ToList())
        {
            var a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
            var val = (string?)Registry.GetValue(a, "", "");
            if (!string.IsNullOrEmpty(val))
            {
                StringBuilder name = new("SAPA.BAK");
                var val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                while (!string.IsNullOrEmpty(val2))
                {
                    name.Append(".BAK");
                    val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                }

                Registry.SetValue(a, name.ToString(), val);
            }

            Registry.SetValue(a, "", "SilverAudioPlayerA");
        }
    }

    
}
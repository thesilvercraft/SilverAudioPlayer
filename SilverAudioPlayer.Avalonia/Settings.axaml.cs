using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Win32;
using System;
using System.Text;

namespace SilverAudioPlayer.Avalonia
{
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private static readonly string[] AssociatedFileTypes = new[] { ".mp3", ".aif", ".aiff", ".flac", ".wav", ".ogg", ".midi", ".mid" };

        public static void RegisterInReg()
        {
            if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty, string.Empty)))
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA", "", "Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA", "FriendlyTypeName", "SilverAudioPlayer Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayerA\\shell\\open\\command", "",
                    $"{Environment.ProcessPath} \"%1\"");
                foreach (string? type in AssociatedFileTypes)
                {
                    string? a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                    string? val = (string?)Registry.GetValue(a, "", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        StringBuilder name = new("SAPA.BAK");
                        string? val2 = (string?)Registry.GetValue(a, name.ToString(), "");
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

        public static void DeleteRegistryFolder(RegistryHive registryHive, string fullPathKeyToDelete)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default))
            {
                baseKey.DeleteSubKeyTree(fullPathKeyToDelete);
            }
        }

        public static void RemoveFromReg()
        {
            if (!string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty, string.Empty)))
            {
                DeleteRegistryFolder(RegistryHive.ClassesRoot, "SilverAudioPlayerA");
                foreach (string? type in AssociatedFileTypes)
                {
                    string? a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                    string? val = (string?)Registry.GetValue(a, "", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        string? val2 = (string?)Registry.GetValue(a, "SAPA.BAK", "");
                        if (!string.IsNullOrEmpty(val2))
                        {
                            Registry.SetValue(a, "", val2);
                        }
                    }
                }
            }
        }

        private void RegisterClick(object? sender, RoutedEventArgs e)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty, string.Empty)))
                {
                    RegisterInReg();
                }
                else
                {
                    RemoveFromReg();
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
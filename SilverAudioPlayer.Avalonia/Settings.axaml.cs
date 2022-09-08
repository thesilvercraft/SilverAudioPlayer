using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
            this.DoAfterInitTasks(true);
            ColorBox = this.FindControl<AutoCompleteBox>("ColorBox");
            ColorBoxPB = this.FindControl<AutoCompleteBox>("ColorBoxPB");

            TransparencyDown = this.FindControl<ComboBox>("TransparencyDown");
            TransparencyDown.SelectedItem = WindowExtensions.GetEnv<WindowTransparencyLevel>("SAPTransparency") ?? WindowTransparencyLevel.AcrylicBlur;
            TransparencyDown.SelectionChanged += TransparencyDown_SelectionChanged;
            DataContext = new
            {
                TransparencyTypes = Enum.GetValues<WindowTransparencyLevel>(),
                AutoSuggestColours = Enum.GetValues<SilverAudioPlayer.Avalonia.KnownColor>(),
            };
            ColorBox.Text = WindowExtensions.GetEnv("SAPColor");
            ColorBoxPB.Text = WindowExtensions.GetEnv("SAPPBColor");

        }

        private void TransparencyDown_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            WindowExtensions.SetEnv("SAPTransparency", Enum.GetName(((WindowTransparencyLevel?)TransparencyDown.SelectedItem)??WindowTransparencyLevel.AcrylicBlur));
            OnNewColor?.Invoke(this, null);
            WindowExtensions.OnStyleChange(this, null);
        }

        public EventHandler OnNewColor;
        internal MainWindow mainWindow;
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
        private void ToggleTransparency(object? sender, RoutedEventArgs e)
        {
            if (WindowExtensions.GetEnv("DisableSAPTransparency") != "true")
            {
                WindowExtensions.SetEnv("DisableSAPTransparency", "true");
                OnNewColor?.Invoke(this, null);
                WindowExtensions.OnStyleChange(this, null);

            }
            else
            {
                WindowExtensions.SetEnv("DisableSAPTransparency", "false");
                OnNewColor?.Invoke(this, null);
                WindowExtensions.OnStyleChange(this, null);


            }
        }
        private void ChangeColorPB(object? sender, RoutedEventArgs e)
        {

            if (Color.TryParse(ColorBoxPB.Text, out Color c))
            {
                WindowExtensions.SetEnv("SAPPBColor", ColorBoxPB.Text);
                mainWindow.SetPBColor(c);

            }
            if (Enum.TryParse(ColorBoxPB.Text, out KnownColor kc))
            {
                WindowExtensions.SetEnv("SAPPBColor", ColorBoxPB.Text);
                mainWindow.SetPBColor(kc.ToColor());
            }
            else if (string.IsNullOrEmpty(ColorBoxPB.Text))
            {
                WindowExtensions.SetEnv("SAPPBColor", null);
                mainWindow.SetPBColor(Colors.Coral);

            }
        }
        private void ChangeColor(object? sender, RoutedEventArgs e)
        {
           
             if (Color.TryParse(ColorBox.Text, out Color c))
            {
                WindowExtensions.SetEnv("SAPColor", ColorBox.Text);
                OnNewColor?.Invoke(this, new());
                WindowExtensions.OnStyleChange(this, null);
            }
            if (Enum.TryParse(ColorBox.Text, out KnownColor kc))
            {
                WindowExtensions.SetEnv("SAPColor", ColorBox.Text);
                OnNewColor?.Invoke(this, new());
                WindowExtensions.OnStyleChange(this, null);
            }
            else if(string.IsNullOrEmpty(ColorBox.Text))
            {
                WindowExtensions.SetEnv("SAPColor", null);
                OnNewColor?.Invoke(this, new());
                WindowExtensions.OnStyleChange(this, null);
            }
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

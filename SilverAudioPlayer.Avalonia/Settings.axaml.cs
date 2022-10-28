using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Win32;

namespace SilverAudioPlayer.Avalonia;

public partial class Settings : Window
{
    internal MainWindow mainWindow;

    public EventHandler OnNewColor;

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
        TransparencyDown.SelectedItem =
            "SAPTransparency".GetEnv<WindowTransparencyLevel>() ?? WindowTransparencyLevel.AcrylicBlur;
        TransparencyDown.SelectionChanged += TransparencyDown_SelectionChanged;
        DataContext = new SettingsDC();
        ColorBox.Text = "SAPColor".GetEnv();
        ColorBoxPB.Text = "SAPPBColor".GetEnv();
    }

    private void TransparencyDown_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Task.Run(() => "SAPTransparency".SetEnv(Enum.GetName((WindowTransparencyLevel?)TransparencyDown.SelectedItem ??
                                                             WindowTransparencyLevel.AcrylicBlur)));
        OnNewColor?.Invoke(this, null);
        WindowExtensions.OnStyleChange(this, null);
    }

    public void RegisterInReg()
    {
        if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                string.Empty)))
        {
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

    public static void DeleteRegistryFolder(RegistryHive registryHive, string fullPathKeyToDelete)
    {
        using (var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Default))
        {
            baseKey.DeleteSubKeyTree(fullPathKeyToDelete);
        }
    }

    public void RemoveFromReg()
    {
        if (!string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                string.Empty)))
        {
            DeleteRegistryFolder(RegistryHive.ClassesRoot, "SilverAudioPlayerA");
            foreach (var type in mainWindow.Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                         .SelectMany(x => x.FileExtensions).ToList())
            {
                var a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                var val = (string?)Registry.GetValue(a, "", "");
                if (!string.IsNullOrEmpty(val))
                {
                    var val2 = (string?)Registry.GetValue(a, "SAPA.BAK", "");
                    if (!string.IsNullOrEmpty(val2)) Registry.SetValue(a, "", val2);
                }
            }
        }
    }

    private void RegisterClick(object? sender, RoutedEventArgs e)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayerA", string.Empty,
                    string.Empty)))
                RegisterInReg();
            else
                RemoveFromReg();
        }
    }

    private void ToggleTransparency(object? sender, RoutedEventArgs e)
    {
        if ("DisableSAPTransparency".GetEnv() != "true")
        {
            Task.Run(() => "DisableSAPTransparency".SetEnv("true"));
            OnNewColor?.Invoke(this, null);
            WindowExtensions.OnStyleChange(this, null);
        }
        else
        {
            Task.Run(() => "DisableSAPTransparency".SetEnv("false"));
            OnNewColor?.Invoke(this, null);
            WindowExtensions.OnStyleChange(this, null);
        }
    }

    private void ChangeColorPB(object? sender, RoutedEventArgs e)
    {
        Task.Run(() => "SAPPBColor".SetEnv(ColorBoxPB.Text));

        mainWindow.SetPBColor("SAPPBColor".ReadBackground(KnownColor.Coral.ToColor()));
    }

    private void ChangeColor(object? sender, RoutedEventArgs e)
    {
        Task.Run(() => "SAPColor".SetEnv(ColorBox.Text));
        OnNewColor?.Invoke(this, new EventArgs());
        WindowExtensions.OnStyleChange(this, null);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

public class SettingsDC
{
    public WindowTransparencyLevel[] TransparencyTypes { get; set; } =
    {
        WindowTransparencyLevel.None, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.Blur,
        WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.ForceAcrylicBlur, WindowTransparencyLevel.Mica
    };

    public KnownColor[] AutoSuggestColours { get; set; } =
    {
        KnownColor.None, KnownColor.Transparent, KnownColor.Black, KnownColor.Navy, KnownColor.DarkBlue,
        KnownColor.MediumBlue, KnownColor.Blue, KnownColor.DarkGreen, KnownColor.Green, KnownColor.Teal,
        KnownColor.DarkCyan, KnownColor.DeepSkyBlue, KnownColor.DarkTurquoise, KnownColor.SilverCraftBlue,
        KnownColor.MediumSpringGreen, KnownColor.Lime, KnownColor.SpringGreen, KnownColor.Aqua, KnownColor.Aqua,
        KnownColor.MidnightBlue, KnownColor.DodgerBlue, KnownColor.LightSeaGreen, KnownColor.ForestGreen,
        KnownColor.SeaGreen, KnownColor.DarkSlateGray, KnownColor.LimeGreen, KnownColor.MediumSeaGreen,
        KnownColor.Turquoise, KnownColor.RoyalBlue, KnownColor.SteelBlue, KnownColor.DarkSlateBlue,
        KnownColor.MediumTurquoise, KnownColor.Indigo, KnownColor.DarkOliveGreen, KnownColor.CadetBlue,
        KnownColor.CornflowerBlue, KnownColor.RebeccaPurple, KnownColor.MediumAquamarine, KnownColor.DimGray,
        KnownColor.SlateBlue, KnownColor.OliveDrab, KnownColor.SlateGray, KnownColor.LightSlateGray,
        KnownColor.MediumSlateBlue, KnownColor.LawnGreen, KnownColor.Chartreuse, KnownColor.Aquamarine,
        KnownColor.Maroon, KnownColor.Purple, KnownColor.Olive, KnownColor.Gray, KnownColor.SkyBlue,
        KnownColor.LightSkyBlue, KnownColor.BlueViolet, KnownColor.DarkRed, KnownColor.DarkMagenta,
        KnownColor.SaddleBrown, KnownColor.DarkSeaGreen, KnownColor.LightGreen, KnownColor.MediumPurple,
        KnownColor.DarkViolet, KnownColor.PaleGreen, KnownColor.DarkOrchid, KnownColor.YellowGreen, KnownColor.Sienna,
        KnownColor.Brown, KnownColor.DarkGray, KnownColor.LightBlue, KnownColor.GreenYellow, KnownColor.PaleTurquoise,
        KnownColor.LightSteelBlue, KnownColor.PowderBlue, KnownColor.Firebrick, KnownColor.DarkGoldenrod,
        KnownColor.MediumOrchid, KnownColor.RosyBrown, KnownColor.DarkKhaki, KnownColor.Silver,
        KnownColor.MediumVioletRed, KnownColor.IndianRed, KnownColor.Peru, KnownColor.Chocolate, KnownColor.Tan,
        KnownColor.LightGray, KnownColor.Thistle, KnownColor.Orchid, KnownColor.Goldenrod, KnownColor.PaleVioletRed,
        KnownColor.Crimson, KnownColor.Gainsboro, KnownColor.Plum, KnownColor.BurlyWood, KnownColor.LightCyan,
        KnownColor.Lavender, KnownColor.DarkSalmon, KnownColor.Violet, KnownColor.PaleGoldenrod, KnownColor.LightCoral,
        KnownColor.Khaki, KnownColor.AliceBlue, KnownColor.Honeydew, KnownColor.Azure, KnownColor.SandyBrown,
        KnownColor.Wheat, KnownColor.Beige, KnownColor.WhiteSmoke, KnownColor.MintCream, KnownColor.GhostWhite,
        KnownColor.Salmon, KnownColor.AntiqueWhite, KnownColor.Linen, KnownColor.LightGoldenrodYellow,
        KnownColor.OldLace, KnownColor.Red, KnownColor.Magenta, KnownColor.Magenta, KnownColor.DeepPink,
        KnownColor.OrangeRed, KnownColor.Tomato, KnownColor.HotPink, KnownColor.Coral, KnownColor.DarkOrange,
        KnownColor.LightSalmon, KnownColor.Orange, KnownColor.LightPink, KnownColor.Pink, KnownColor.Gold,
        KnownColor.PeachPuff, KnownColor.NavajoWhite, KnownColor.Moccasin, KnownColor.Bisque, KnownColor.MistyRose,
        KnownColor.BlanchedAlmond, KnownColor.PapayaWhip, KnownColor.LavenderBlush, KnownColor.SeaShell,
        KnownColor.Cornsilk, KnownColor.LemonChiffon, KnownColor.FloralWhite, KnownColor.Snow, KnownColor.Yellow,
        KnownColor.LightYellow, KnownColor.Ivory, KnownColor.White
    };
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Microsoft.Win32;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.ConfigScreen;
using SilverCraft.AvaloniaUtils;
using SilverMagicBytes;
using Swordfish.NET.Collections.Auxiliary;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using KnownColor = SilverCraft.AvaloniaUtils.KnownColor;

namespace SilverAudioPlayer.Avalonia;
public record InfoPRecord(string Name, string Description, Version? Version, IImage? Icon, string Licenses,
    ICodeInformation Item, bool Configurable, bool IsPlayStreamProvider, bool IsAskingMemoryProvider, bool IsSyncPlugin);
public partial class Settings : Window
{
    internal MainWindow mainWindow;


    public Settings()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        this.DoAfterInitTasksF();
    }
    public Settings(MainWindow mainWindow) : this()
    {

        ColorBox = this.FindControl<AutoCompleteBox>("ColorBox");
        ColorBoxPB = this.FindControl<AutoCompleteBox>("ColorBoxPB");
        TransparencyDown = this.FindControl<ComboBox>("TransparencyDown");
        CapBox = this.FindControl<ListBox>("CapBox");
        this.mainWindow = mainWindow;

        TransparencyDown.SelectedItem =
            WindowExtensions.envBackend.GetEnum<WindowTransparencyLevel>("SAPTransparency") ?? WindowTransparencyLevel.AcrylicBlur;
        TransparencyDown.SelectionChanged += TransparencyDown_SelectionChanged;
        ColorBox.Text = WindowExtensions.envBackend.GetString("SAPColor");
        ColorBoxPB.Text = WindowExtensions.envBackend.GetString("SAPPBColor");
        List<ICodeInformation> info = new();
        info.AddRange(mainWindow.Logic.PlayProviders);
        info.AddRange(mainWindow.Logic.MusicStatusInterfaces);
        info.AddRange(mainWindow.Logic.MetadataProviders);
        info.AddRange(mainWindow.Logic.WakeLockInterfaces);
        info.AddRange(mainWindow.Logic.PlayStreamProviders);
        info.AddRange(mainWindow.Logic.SyncPlugins);
        info.Add(mainWindow.Env);
        var ir = GetInfoRecords(info);
        DataContext = new SettingsDC() { Items = ir.Item1,
            ProductName = mainWindow.Env.Name,
            ProductDescription = mainWindow.Env.Description,
            ProductIcon = ir.Item1.Last().Icon,
        };
        Legalese = ir.Item2;
    }
    string Legalese;
    public static Tuple<ObservableCollection<InfoPRecord>, string> GetInfoRecords(List<ICodeInformation> info)
    {
        ObservableCollection<InfoPRecord> infop = new();
        StringBuilder licenses = new();
        foreach (var item in info)
        {
            IImage? icon = DecodeImage(item.Icon);
           
            infop.Add(new InfoPRecord(
                item.Name,
                item.Description,
                item.Version,
                icon,
                item.Licenses,
                item,
                item is IAmConfigurable,
                item is IPlayStreamProvider,
                item is IAmOnceAgainAskingYouForYourMemory,
                item is ISyncPlugin
            ));
            licenses.AppendLine(item.Licenses);
        }
        return new(infop, licenses.ToString());
    }
    public static IImage? DecodeImage(WrappedStream? stream, int width=80)
    {
        if (stream != null)
        {
            stream.GetStream();
            Debug.WriteLine(stream.MimeType);
            if (stream.MimeType == KnownMimes.JPGMime || stream.MimeType == KnownMimes.PngMime)
            {
                return Bitmap.DecodeToWidth(stream.GetStream(), width);
            }
            else if (stream.MimeType == KnownMimes.SVGMime)
            {
                var img = new SvgImage
                {
                    Source = new()
                };
                img.Source.Load(stream.GetStream());
                return img;
            }
        }
        return null;
    }
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

    public void ElementDoubleTapped(object _, global::Avalonia.Input.TappedEventArgs args)
    {
        var y = (InfoPRecord?)CapBox.SelectedItem;
        ShowElementActionWindow(y, mainWindow);
    }
    public void ConfigureClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y != null && y.DataContext is InfoPRecord record && record.Configurable)
        {
            if (record.Item is IAmConfigurable configurable)
            {
                ConfigureWindow cw = new();
                cw.HandleConfiguration(configurable);
                cw.Show();
            }
        }
    }
    public void OpenConfigFileClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y != null && y.DataContext is InfoPRecord record && record.IsAskingMemoryProvider && record.Item is IAmOnceAgainAskingYouForYourMemory configurable && mainWindow.Logic.MemoryProvider is IWillTellYouWhereIStoreTheConfigs l)
        {
            LaunchActionsWindow launchActionsWindow = new();
            List<SAction> actions = new();
            foreach (var ob in configurable.ObjectsToRememberForMe)
            {
                var m = l.GetConfig(ob.Id);
                actions.Add(new SAction
                {
                    ActionName = "Open " + m,
                    ActionToInvoke = () =>
                    {
                        OpenBrowser(m);
                    }
                });
            }
            launchActionsWindow.AddActions(actions);
            launchActionsWindow.Show();
        }
    }
    public void PlayProviderClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y is not { DataContext: InfoPRecord record } ||
            record is { IsPlayStreamProvider: false, IsSyncPlugin: false }) return;
        switch (record.Item)
        {
            case IPlayStreamProvider configurable:
                configurable.Use(mainWindow.Env);
                break;
            case ISyncPlugin syncPlugin:
                syncPlugin.Use(mainWindow.Env);
                break;
        }
    }
    
    public static string GetHumanName(URLType type)
    {
        return type switch
        {
            URLType.Unknown => string.Empty,
            URLType.Code => "code",
            URLType.LibraryCode => "library code",
            URLType.Documentation => "documentation",
            URLType.LibraryDocumentation => "library documentation",
            URLType.PackageManager => "package listing",
            _ => string.Empty,
        };
    }
    public static string GetHumanerName(URLType type)
    {
        var name = GetHumanName(type);
        if(!string.IsNullOrEmpty(name))
        {
            return " (" + name + ")";
        }
        return name;
    }
    public static void ShowElementActionWindow(InfoPRecord element, MainWindow mainWindow)
    {

        LaunchActionsWindow launchActionsWindow = new();
        List<SAction> actions = new();
        if(element.Item.Links==null)
        {
            return;
        }
        foreach (var z in element.Item.Links)
        {
            actions.Add(new SAction
            { ActionName = "Open " + z.Item1 + GetHumanerName(z.Item2), ActionToInvoke = () => OpenBrowser(z.Item1.ToString()) });

        }
        if (element.Item is IAmConfigurable configurable)
            actions.Add(new SAction
            {
                ActionName = "🔧Configure",
                ActionToInvoke = () =>
                {
                    ConfigureWindow cw = new();
                    cw.HandleConfiguration(configurable);
                    cw.Show();
                }
            });
        if(element.Item is IPlayStreamProvider streamProvider)
        {
            actions.Add(new SAction
            {
                ActionName = "Use",
                ActionToInvoke = () =>
                {
                    streamProvider.Use(mainWindow.Env);
                }
            });
        }
        else if (element.Item is ISyncPlugin syncPlugin)
        {
            actions.Add(new SAction
            {
                ActionName = "Sync",
                ActionToInvoke = () =>
                {
                    syncPlugin.Use(mainWindow.Env);
                }
            });
        }
        
        launchActionsWindow.AddActions(actions);
        launchActionsWindow.Show();
    }
    private void TransparencyDown_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Task.Run(() => WindowExtensions.envBackend.SetString("SAPTransparency",(Enum.GetName((WindowTransparencyLevel?)TransparencyDown.SelectedItem ??
                                                             WindowTransparencyLevel.AcrylicBlur))));
        WindowExtensions.OnStyleChange.Invoke(this, new(null,null, (WindowTransparencyLevel?)TransparencyDown.SelectedItem ??
                                                             WindowTransparencyLevel.AcrylicBlur));
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
    private void LicenseInfo(object? sender, RoutedEventArgs e)
    {
        var w = new Window();
        TextBox tb = new()
        {
            IsReadOnly = true,
            Text = Legalese,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        w.Content = tb;
        w.DoAfterInitTasksF();
        w.Show();

    }
    private void ToggleTransparency(object? sender, RoutedEventArgs e)
    {
        bool SapTransparency = WindowExtensions.envBackend.GetBool("DisableSAPTransparency") != true;
        Task.Run(() => WindowExtensions.envBackend.SetBool("DisableSAPTransparency",SapTransparency));
        WindowExtensions.OnStyleChange(this, new(SapTransparency, null, null));
    }
    
    public static readonly GradientStops defPBStops = new()
    {
        new(KnownColor.Coral.ToColor(), 0),
        new(KnownColor.SilverCraftBlue.ToColor(), 1)
    };
    private void ChangeColorPB(object? sender, RoutedEventArgs e)
    {
        Task.Run(() => WindowExtensions.envBackend.SetEnv("SAPPBColor",ColorBoxPB.Text));
       
        mainWindow.SetPBColor(ColorBoxPB.Text.ParseBackground(new LinearGradientBrush() { GradientStops = defPBStops }));

    }

    private void ChangeColor(object? sender, RoutedEventArgs e)
    {
        Task.Run(() => { WindowExtensions.envBackend.SetString("SAPColor",ColorBox.Text); });
        WindowExtensions.OnStyleChange?.Invoke(this, new(null, ColorBox.Text, null));
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
    public ObservableCollection<InfoPRecord> Items { get; set; }
    public string ProductName { get; internal set; }
    public string ProductDescription { get; internal set; }
    public IImage? ProductIcon { get; internal set; }
}
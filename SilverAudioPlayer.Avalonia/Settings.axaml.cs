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
    ICodeInformation Item, bool Configurable, bool IsPlayStreamProvider, bool IsAskingMemoryProvider, bool IsSyncPlugin,bool IsMusicStatusInterface);
public partial class Settings : Window
{
    internal MainWindow mainWindow;


    public Settings()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        MainGrid = this.FindControl<Grid>("MainGrid");
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
        this.DoAfterInitTasksF();
    }
    public Settings(MainWindow mainWindow) : this()
    {

        ColorBox = this.FindControl<AutoCompleteBox>("ColorBox");
        ColorBoxPB = this.FindControl<AutoCompleteBox>("ColorBoxPB");
        TransparencyDown = this.FindControl<ComboBox>("TransparencyDown");
        CapBox = this.FindControl<ListBox>("CapBox");
        this.mainWindow = mainWindow;
      //  TransparencyDown.SelectedItem =
   //         WindowExtensions.envBackend.GetEnum<WindowTransparencyLevel>("SAPTransparency") ?? WindowTransparencyLevel.AcrylicBlur;
        TransparencyDown.SelectionChanged += TransparencyDown_SelectionChanged;
        ColorBox.Text = WindowExtensions.envBackend.GetString("SAPColor");
        ColorBoxPB.Text = WindowExtensions.envBackend.GetString("SAPPBColor");
        List<ICodeInformation> info = new()
        {
            mainWindow.Env
        };
        info.AddRange(mainWindow.Logic.PlayProviders);
        info.AddRange(mainWindow.Logic.MusicStatusInterfaces);
        info.AddRange(mainWindow.Logic.MetadataProviders);
        info.AddRange(mainWindow.Logic.WakeLockInterfaces);
        info.AddRange(mainWindow.Logic.PlayStreamProviders);
        info.AddRange(mainWindow.Logic.SyncPlugins);
        var ir = GetInfoRecords(info);
        DataContext = new SettingsDC() { Items = ir.Item1,
            ProductName = mainWindow.Env.Name,
            ProductDescription = mainWindow.Env.Description,
            ProductIcon = ir.Item1.First().Icon,
        };
        Legalese = ir.Item2;
    }

    private void EnableMSI(object? sender, RoutedEventArgs e)
    {
        var y = (CheckBox?)sender;
        if (y is not { DataContext: InfoPRecord { IsMusicStatusInterface: true } record }) return;
        if (record.Item is not IMusicStatusInterface msi) return;
        if (y.IsChecked == msi.IsStarted) return;
            if(y.IsChecked==true)
            {
            msi.StartIPC(mainWindow.Env);
            }
            else
            {
            msi.StopIPC(mainWindow.Env);
            }
    }

    string Legalese;
    public static Tuple<ObservableCollection<InfoPRecord>, string> GetInfoRecords(List<ICodeInformation> info)
    {
        ObservableCollection<InfoPRecord> infop = new();
        StringBuilder licenses = new();
        foreach (var item in info)
        {
            var icon = DecodeImage(item.Icon);
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
                item is ISyncPlugin,
                item is IMusicStatusInterface
            ));
            licenses.AppendLine(item.Licenses);
        }
        return new(infop, licenses.ToString());
    }
    public static IImage? DecodeImage(WrappedStream? stream, int width=80)
    {
        if (stream == null) return null;
        stream.Use((str) => { });
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
        if (y is not { DataContext: InfoPRecord { Configurable: true } record }) return;
        if (record.Item is not IAmConfigurable configurable) return;
        ConfigureWindow cw = new();
        cw.HandleConfiguration(configurable);
        cw.Show();
    }
    public void OpenConfigFileClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y is not { DataContext: InfoPRecord { IsAskingMemoryProvider: true, Item: IAmOnceAgainAskingYouForYourMemory configurable } } ||
            mainWindow.Logic.MemoryProvider is not IWillTellYouWhereIStoreTheConfigs l) return;
        LaunchActionsWindow launchActionsWindow = new();
        var actions = configurable.ObjectsToRememberForMe.Select(ob => l.GetConfig(ob.Id)).Select(m => new SAction { ActionName = "Open " + m, ActionToInvoke = () => { OpenBrowser(m); } }).ToList();
        launchActionsWindow.AddActions(actions);
        launchActionsWindow.Show();
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
        if (element == null) return;
        LaunchActionsWindow launchActionsWindow = new();
        if(element.Item.Links==null)
        {
            return;
        }
        var actions = element.Item.Links.Select(z => new SAction { ActionName = "Open " + z.Item1 + GetHumanerName(z.Item2), ActionToInvoke = () => OpenBrowser(z.Item1.ToString()) }).ToList();
        switch (element.Item)
        {
            case IAmConfigurable configurable:
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
                break;
            case IPlayStreamProvider streamProvider:
                actions.Add(new SAction
                {
                    ActionName = "🔨Use",
                    ActionToInvoke = () =>
                    {
                        streamProvider.Use(mainWindow.Env);
                    }
                });
                break;
            case ISyncPlugin syncPlugin:
                actions.Add(new SAction
                {
                    ActionName = "🔁Sync",
                    ActionToInvoke = () =>
                    {
                        syncPlugin.Use(mainWindow.Env);
                    }
                });
                break;
        }

        launchActionsWindow.AddActions(actions);
        launchActionsWindow.Show();
    }
    private void TransparencyDown_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Task.Run(() => WindowExtensions.envBackend.SetString("SAPTransparency",TransparencyDown.SelectedItem.ToString() ??
            WindowTransparencyLevel.AcrylicBlur.ToString()));
        WindowExtensions.OnStyleChange.Invoke(this, new(null,null, new []{(WindowTransparencyLevel)TransparencyDown.SelectedItem} ));
    }
    
    private void RegisterClick(object? sender, RoutedEventArgs e)
    {
        if (RegistryRegistration.IsReg())
        {
            RegistryRegistration.UnRegisterUrlScheme(mainWindow);
        }
        else
        {
            RegistryRegistration.RegisterUrlScheme(mainWindow);
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
        var SapTransparency = WindowExtensions.envBackend.GetBool("DisableSAPTransparency") != true;
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
        var t = ColorBoxPB.Text;
        Task.Run(() => WindowExtensions.envBackend.SetEnv("SAPPBColor",t));
       
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
        WindowTransparencyLevel.AcrylicBlur,  WindowTransparencyLevel.Mica
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
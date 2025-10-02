using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;
using SilverCraft.Ekorensis;
using SilverCraft.Shared;
using SilverMagicBytes;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using KnownColor = SilverCraft.AvaloniaUtils.KnownColor;

namespace Catenoid;

public record InfoPRecord(string Name, string Description, Version? Version, IImage? Icon, string Licenses,
    ICodeInformation Item, bool Configurable, bool IsPlayStreamProvider, bool IsAskingMemoryProvider, bool IsSyncPlugin,
    bool IsMusicStatusInterface);
public static class ConstStrings
{
    public const string Lrc = "SAPLRCColor";
    public const string BG = "SAPColor";
    public const string PB = "SAPPBColor";
    public const string DisableTransparency = "DisableSAPTransparency";
    public const string TransparencyC = "SAPTransparency";

}
public partial class Settings : Window
{
    internal MainWindow mainWindow;
    private ILogger? Logger;

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

        Logger = SilverAudioPlayer.Shared.Logger.GetLogger(typeof(Settings));
        this.DoAfterInitTasksF();
    }

    public Settings(MainWindow mainWindow) : this()
    {
        ColorBox = this.FindControl<AutoCompleteBox>("ColorBox");
        ColorBoxPB = this.FindControl<AutoCompleteBox>("ColorBoxPB");
        ColorBoxLRC = this.FindControl<AutoCompleteBox>("ColorBoxLRC");

        TransparencyText = this.FindControl<TextBox>("TransparencyText");
        CapBox = this.FindControl<ListBox>("CapBox");
        ProductIconButton = this.FindControl<Button>("ProductIconButton");
        AutoSaveCheckBox = this.FindControl<CheckBox>("AutoSaveCheckBox");
        ProductIconButton.AddHandler(InputElement.PointerReleasedEvent, PointerReleasedProductIcon,
            RoutingStrategies.Tunnel, true);
        this.mainWindow = mainWindow;
        TransparencyText.Text = WindowExtensions.envBackend.GetString(ConstStrings.TransparencyC);

        TransparencyText.TextChanged += TransparencyTextChanged;
    

        List<ICodeInformation> info =
        [
            mainWindow.Env, .. (mainWindow.Logic as ILogicImportable).PluginCodeInformation
        ];
        var ir = GetInfoRecords(info);
        _settingsDc = new SettingsDC(this)
        {
            Items = ir.Item1,
            ProductName = mainWindow.Env.Name,
            ProductDescription = mainWindow.Env.Description,
            ProductIcon = ir.Item1.First().Icon,
            Config = mainWindow.config
        };

        DataContext = _settingsDc;
        Legalese = ir.Item2;
    }
    private void TransparencyTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = TransparencyText.Text;
        Task.Run(() => WindowExtensions.envBackend.SetString(ConstStrings.TransparencyC,
            text ??
           WindowTransparencyLevel.AcrylicBlur.ToString()));
        if (text is not null)
        {
            WindowExtensions.OnStyleChange.Invoke(this,
                        new(null, WindowExtensions.GetTransparencyLevelsFromString(text), null));
        }
    }

  

    private SettingsDC _settingsDc;

    private void EnableMSI(object? sender, RoutedEventArgs e)
    {
        var y = (CheckBox?)sender;
        if (y is not { DataContext: InfoPRecord { IsMusicStatusInterface: true } record }) return;
        if (record.Item is not IMusicStatusInterface msi) return;
        if (y.IsChecked == msi.IsStarted) return;
        if (y.IsChecked == true)
        {
            mainWindow.config.BlockedPlugins.Remove(msi.GetType().FullName);
            ((ICanBeToldThatAPartOfMeIsChanged)mainWindow.config).PropertyChanged(mainWindow.config, new PropertyChangedEventArgs("BlockedPlugins"));
            msi.StartIPC(mainWindow.Env);
        }
        else
        {
            mainWindow.config.BlockedPlugins.Add(msi.GetType().FullName);
            ((ICanBeToldThatAPartOfMeIsChanged)mainWindow.config).PropertyChanged(mainWindow.config, new PropertyChangedEventArgs("BlockedPlugins"));
            msi.StopIPC(mainWindow.Env);
        }
    }

    string Legalese;

    public static Tuple<ObservableCollection<InfoPRecord>, string> GetInfoRecords(List<ICodeInformation> info)
    {
        ObservableCollection<InfoPRecord> infop = [];
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
                item is IAmConfigurable or IConfigurableUiPlugin,
                item is IPlayStreamProvider,
                item is IAmOnceAgainAskingYouForYourMemory or IHaveConfigFilesWithKnownLocations,
                item is ISyncPlugin,
                item is IMusicStatusInterface
            ));
            licenses.AppendLine(item.Licenses);
        }

        return new(infop, licenses.ToString());
    }

    public static IImage? DecodeImage(WrappedStream? stream, int width = 80)
    {
        if (stream == null) return null;
        stream.Use((_) => { });
        if (stream.MimeType == KnownMimes.JPGMime || stream.MimeType == KnownMimes.PngMime)
        {
            return Bitmap.DecodeToWidth(stream.GetStream(), width);
        }

        if (stream.MimeType == KnownMimes.SVGMime)
        {
            return new SvgImage
            {
                Source = SvgSource.LoadFromStream(stream.GetStream())
            };
        }
        return null;
    }


    public void ElementDoubleTapped(object _, TappedEventArgs args)
    {
        var y = (InfoPRecord?)CapBox.SelectedItem;
        ShowElementActionWindow(y, mainWindow);
    }

    public void ConfigureClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y is not { DataContext: InfoPRecord { Configurable: true } record }) return;
        switch (record.Item)
        {
            case IConfigurableUiPlugin configurableUiPlugin:
                configurableUiPlugin.ConfigureUi(mainWindow.Env);
                break;
            case IAmConfigurable configurable:
                {
                    EkorensisUtils.LaunchConfigurableWindow(configurable);
                    break;
                }
        }
    }

    public void OpenConfigFileClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y is { DataContext: InfoPRecord { IsAskingMemoryProvider: true, Item: IHaveConfigFilesWithKnownLocations knownLocations } })
        {
            ShowLaunchActionsFiles(knownLocations.KnownConfigFileLocations);
            return;
        }
        if (y is not
            {
                DataContext: InfoPRecord
                {
                    IsAskingMemoryProvider: true, Item: IAmOnceAgainAskingYouForYourMemory configurable
                }
            } ||
            mainWindow.Logic.MemoryProvider is not IWillTellYouWhereIStoreTheConfigs l) return;
        ShowLaunchActionsFiles(configurable.ObjectsToRememberForMe.Select(ob => l.GetConfig(ob.Id)));

    }
    void ShowLaunchActionsFiles(IEnumerable<string> files, bool folder = true)
    {
        ShowLaunchActionsWindow(files.Select(m =>
                new SAction
                {
                    ActionName = "Open " + m,
                    ActionToInvoke = () =>
                    {
                        if (folder)
                        {
                            EnvironmentExtensions.OpenFolder(m);
                        }
                        else
                        {
                            EnvironmentExtensions.OpenBrowser(m);
                        }
                    }
                })
            .ToList());
    }
    static void ShowLaunchActionsWindow(List<SAction> actions)
    {
        Dispatcher.UIThread.Post(() =>
      {
          LaunchActionsWindow launchActionsWindow = new(actions);
          launchActionsWindow.Show();
      }, DispatcherPriority.Normal);
    }
    public void PlayProviderClick(object button, RoutedEventArgs args)
    {
        var y = (Button?)button;
        if (y is not { DataContext: InfoPRecord record } ||
            record is { IsPlayStreamProvider: false, IsSyncPlugin: false }) return;
        switch (record.Item)
        {
            case IPlayStreamProvider streamProvider:
                streamProvider.Use(mainWindow.Env);
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
        if (!string.IsNullOrEmpty(name))
        {
            return " (" + name + ")";
        }

        return name;
    }

    public static void ShowElementActionWindow(InfoPRecord? element, MainWindow mainWindow)
    {
        if (element?.Item.Links == null)
        {
            return;
        }

        var actions = element.Item.Links.Select(z => new SAction
        {
            ActionName = "Open " + z.Item1 + GetHumanerName(z.Item2),
            ActionToInvoke = () => EnvironmentExtensions.OpenBrowser(z.Item1.ToString())
        }).ToList();
        switch (element.Item)
        {
            case IConfigurableUiPlugin guiConfig:
                actions.Add(new SAction
                {
                    ActionName = "🔧Configure",
                    ActionToInvoke = () => { guiConfig.ConfigureUi(mainWindow.Env); }
                });
                break;
            case IAmConfigurable configurable:
                actions.Add(new SAction
                {
                    ActionName = "🔧Configure",
                    ActionToInvoke = () =>
                    {
                        EkorensisUtils.LaunchConfigurableWindow(configurable);
                    }
                });
                break;
            case IPlayStreamProvider streamProvider:
                actions.Add(new SAction
                {
                    ActionName = "🔨Use",
                    ActionToInvoke = () => { streamProvider.Use(mainWindow.Env); }
                });
                break;
            case ISyncPlugin syncPlugin:
                actions.Add(new SAction
                {
                    ActionName = "🔁Sync",
                    ActionToInvoke = () => { syncPlugin.Use(mainWindow.Env); }
                });
                break;
        }
        if (element.Item is IExportCommands commands)
        {
            foreach (var command in commands.GetCommands())
            {
                actions.Add(new SAction
                {
                    ActionName = command.Name,
                    ActionToInvoke = () => { commands.RaiseCommand(command); }
                });
            }
        }
        ShowLaunchActionsWindow(actions);
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
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        w.Content = tb;
        w.DoAfterInitTasksF();
        w.Show();
    }

    private void ToggleTransparency(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var SapTransparency = WindowExtensions.envBackend.GetBool(ConstStrings.DisableTransparency) != true;
            WindowExtensions.OnStyleChange(this, new(null, null, null));
            WindowExtensions.envBackend.SetBool(ConstStrings.DisableTransparency, SapTransparency);
        }, DispatcherPriority.Normal);
    }

    public static readonly GradientStops defPBStops =
    [
        new(KnownColor.Coral.ToColor(), 0),
        new(KnownColor.SilverCraftBlue.ToColor(), 1)
    ];




    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private string? IconTemplate = null;

    private void ProductIconClicked()
    {
        if ((mainWindow.Env.Icon is not { MimeType: SVGMime }) || mainWindow.Env.Icon?.MimeType == null)
        {
            var instance = mainWindow.Env.Icon;
            instance?.Use(x => { });
            if (instance?.MimeType is not SVGMime)
            {
                Logger?.Error("An instance of mainWindow.Env.Icon's mime is not an SVGMime, it is a {@MimeType}",
                    instance?.MimeType);
                return;
            }
        }


        if (IconTemplate == null)
        {
            mainWindow.Env.Icon?.Use(s =>
            {
                using var textreader = new StreamReader(s);
                IconTemplate = textreader.ReadToEnd();
                IconTemplate = IconTemplate.Replace("stop-color=\"#6969ff\"", "stop-color=\"{0}\"")
                    .Replace("stop-color=\"#696969\"", "stop-color=\"{1}\"")
                    .Replace("fill=\"#F90\"", "fill=\"{2}\"")
                    .Replace("fill=\"#0F6\"", "fill=\"{3}\"")
                    .Replace("fill=\"#00F0FF\"", "fill=\"{4}\"")
                    .Replace("fill=\"#000\"", "fill=\"{5}\"");
            });
        }

        if (IconTemplate == null) return;
        {
            var realisedTemplate = string.Format(IconTemplate, EnvironmentExtensions.NewHexString(),
                EnvironmentExtensions.NewHexString(), EnvironmentExtensions.NewHexString(),
                EnvironmentExtensions.NewHexString(), EnvironmentExtensions.NewHexString(),
                EnvironmentExtensions.NewHexString());
            _settingsDc.ProductIcon = new SvgImage()
            {
                Source =  SvgSource.LoadFromSvg(realisedTemplate)
            };
            CurrentSource = realisedTemplate;
        }
    }

    private void PointerReleasedProductIcon(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            Clipboard?.SetTextAsync(CurrentSource);
            SAPAWindowExtensions.Toast(SAPAWindowExtensions.CopiedToClipboard);
        }
    }

    private string? CurrentSource;

    private void ProductIconClick(object? sender, RoutedEventArgs e)
    {
        ProductIconClicked();
    }
}


public class SettingsDC(Settings settings) : ReactiveObject
{
    public WindowTransparencyLevel[] TransparencyTypes { get; set; } =
    [
        WindowTransparencyLevel.None, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.Blur,
        WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Mica
    ];

    public string _backgroundColour = WindowExtensions.envBackend.GetString(ConstStrings.BG) ?? "";
    public string BackgroundColour
    {
        get =>_backgroundColour;
        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException(nameof(BackgroundColour), "Not a valid colour");
            }
            this.RaiseAndSetIfChanged(ref _backgroundColour, value); 
            WindowExtensions.envBackend.SetString(ConstStrings.BG, _backgroundColour);
            Dispatcher.UIThread.Post(() =>
            {
                WindowExtensions.OnStyleChange?.Invoke(this, new(_backgroundColour, null, null));
            }, DispatcherPriority.Normal);
        }
    }
    
    public string _progressBarColour = WindowExtensions.envBackend.GetString(ConstStrings.PB) ?? "";
    public string ProgressBarColour
    {
        get =>_progressBarColour;
        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException(nameof(ProgressBarColour), "Not a valid colour");
            }
            this.RaiseAndSetIfChanged(ref _progressBarColour, value); 
            WindowExtensions.envBackend.SetEnv(ConstStrings.PB, _progressBarColour);
            Dispatcher.UIThread.Post(() =>
            {
                settings.mainWindow.SetProgressBarColour(_progressBarColour.ParseBackground(new LinearGradientBrush()
                    { GradientStops = Settings.defPBStops }));
            }, DispatcherPriority.Normal);
        }
    }
    public string _lyricsColour = WindowExtensions.envBackend.GetString(ConstStrings.Lrc) ?? "";
    public string LyricsColour
    {
        get =>_lyricsColour;
        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException(nameof(LyricsColour), "Not a valid colour");
            }
            this.RaiseAndSetIfChanged(ref _lyricsColour, value); 
            WindowExtensions.envBackend.SetEnv(ConstStrings.Lrc, _lyricsColour);
        }
    }

   
    public KnownColor[] AutoSuggestColours { get; set; } =
    [
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
    ];

    public ObservableCollection<InfoPRecord> Items { get; set; }
    public string ProductName { get; internal set; }
    public string ProductDescription { get; internal set; }
    public Config Config { get; set; }

    public IImage? ProductIcon
    {
        get => _productIcon;
        set => this.RaiseAndSetIfChanged(ref _productIcon, value, nameof(ProductIcon));
    }

    public IImage? _productIcon;
}
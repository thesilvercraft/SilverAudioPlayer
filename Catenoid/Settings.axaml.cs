using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
using SvgImage = Avalonia.Svg.Skia.SvgImage;

namespace Catenoid;

public record InfoPRecord(
    string Name,
    string Description,
    Version? Version,
    IImage? Icon,
    string Licenses,
    ICodeInformation Item,
    bool Configurable,
    bool IsPlayStreamProvider,
    bool IsAskingMemoryProvider,
    bool IsSyncPlugin,
    bool IsMusicStatusInterface,
    object? RawIcon = null
);

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
    internal MainWindow? mainWindow;
    private SettingsDC? _settingsDc;
    private string Legalese = string.Empty;

    public Settings()
    {
        InitializeComponent();
        MainGrid = this.FindControl<Grid>("MainGrid");
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

        ProductIconButton?.AddHandler(InputElement.PointerReleasedEvent, PointerReleasedProductIcon,
            RoutingStrategies.Tunnel, true);
        this.mainWindow = mainWindow;
        if (TransparencyText != null)
        {
            TransparencyText.Text = WindowExtensions.envBackend.GetString(ConstStrings.TransparencyC);
            TransparencyText.TextChanged += TransparencyTextChanged;
        }

        List<ICodeInformation> info =
        [
            mainWindow.Env, .. (mainWindow.Logic as ILogicImportable).PluginCodeInformation
        ];

        _settingsDc = new SettingsDC(this)
        {
            ProductName = mainWindow.Env.Name,
            ProductDescription = mainWindow.Env.Description,
            Config = mainWindow.config
        };
        DataContext = _settingsDc;
        _ = LoadSettingsDataAsync(info);
    }

    private async Task LoadSettingsDataAsync(List<ICodeInformation> info)
    {
        var (rawRecords, legalese) = await GetInfoRecordsAsync(info);

        Dispatcher.UIThread.Post(() =>
        {
            if (_settingsDc == null) return;
            foreach (var record in rawRecords)
            {
                var uiIcon = record.RawIcon switch
                {
                    SvgSource svgSource => new SvgImage { Source = svgSource },
                    IImage img => img,
                    _ => null
                };
                _settingsDc.Items.Add(record with { Icon = uiIcon });
            }

            _settingsDc.ProductIcon = _settingsDc.Items.FirstOrDefault()?.Icon;
            Legalese = legalese;
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        if (TransparencyText != null)
        {
            TransparencyText.TextChanged -= TransparencyTextChanged;
        }
        if (_settingsDc != null)
        {
            foreach (var item in _settingsDc.Items)
            {
                if (item.Icon is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _settingsDc.Items.Clear();
            _settingsDc.ProductIcon = null;
        }
        base.OnClosed(e);
    }

    private void TransparencyTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = TransparencyText?.Text;

        Task.Run(() => WindowExtensions.envBackend.SetString(ConstStrings.TransparencyC,
            text ??
            WindowTransparencyLevel.AcrylicBlur.ToString()));

        if (text is not null)
        {
            WindowExtensions.OnStyleChange?.Invoke(this,
                new(null, WindowExtensions.GetTransparencyLevelsFromString(text), null, null, null));
        }
    }

    private void EnableMSI(object? sender, RoutedEventArgs e)
    {
        if (mainWindow == null) return;
        var y = (CheckBox?)sender;
        if (y is not { IsFocused: true }) return;
        if (y is not { DataContext: IMusicStatusInterface msi }) return;
        if (y.IsChecked == msi.IsStarted) return;
        if (msi.GetType().FullName is not { } fullName) return;

        if (y.IsChecked == true)
        {
            mainWindow.config.BlockedPlugins.Remove(fullName);

            ((ICanBeToldThatAPartOfMeIsChanged)mainWindow.config).PropertyChanged(mainWindow.config,
                new PropertyChangedEventArgs("BlockedPlugins"));
            msi.StartIPC(mainWindow.Env);
        }
        else
        {
            mainWindow.config.BlockedPlugins.Add(fullName);

            ((ICanBeToldThatAPartOfMeIsChanged)mainWindow.config).PropertyChanged(mainWindow.config,
                new PropertyChangedEventArgs("BlockedPlugins"));
            msi.StopIPC(mainWindow.Env);
        }
    }

    public static async Task<(List<InfoPRecord> Records, string Legalese)> GetInfoRecordsAsync(
        List<ICodeInformation> info)
    {
        var recordsTasks = info.Select(item => Task.Run(() =>
        {
            using var iconStream = item.Icon;
            var icon = DecodeImage(iconStream);

            return new InfoPRecord(
                item.Name,
                item.Description,
                item.Version,
                icon as IImage,
                item.Licenses,
                item,
                item is IAmConfigurable or IConfigurableUiPlugin,
                item is IPlayStreamProvider,
                item is IAmOnceAgainAskingYouForYourMemory or IHaveConfigFilesWithKnownLocations,
                item is ISyncPlugin,
                item is IMusicStatusInterface,
                icon
            );
        }));
        var records = (await Task.WhenAll(recordsTasks)).ToList();
        var licenses = new StringBuilder();

        foreach (var item in info)
        {
            licenses.AppendLine(item.Licenses);
        }

        return (records, licenses.ToString());
    }

    public static object? DecodeImage(WrappedStream? stream, int width = 80)
    {
        if (stream == null) return null;
        stream.Use((_) => { });

        if (stream.MimeType == KnownMimes.JPGMime || stream.MimeType == KnownMimes.PngMime)
        {
            using var imgStream = stream.GetStream();
            return Bitmap.DecodeToWidth(imgStream, width);
        }

        if (stream.MimeType == KnownMimes.SVGMime)
        {
            using var svgStream = stream.GetStream();
            return SvgSource.LoadFromStream(svgStream);
        }

        return null;
    }

    public void ElementDoubleTapped(object _, TappedEventArgs args)
    {
        if (mainWindow == null) return;
        var y = (InfoPRecord?)CapBox?.SelectedItem;
        ShowElementActionWindow(y, mainWindow);
    }

    public void ConfigureClick(object button, RoutedEventArgs args)
    {
        if (mainWindow == null) return;
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
        if (mainWindow == null) return;
        var y = (Button?)button;

        if (y is
            {
                DataContext: InfoPRecord
                {
                    IsAskingMemoryProvider: true, Item: IHaveConfigFilesWithKnownLocations knownLocations
                }
            })
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
        if (mainWindow == null) return;
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
                    ActionToInvoke = () => { EkorensisUtils.LaunchConfigurableWindow(configurable); }
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
        if (mainWindow == null) return;
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
            WindowExtensions.envBackend.SetBool(ConstStrings.DisableTransparency, SapTransparency);
            WindowExtensions.OnStyleChange?.Invoke(this, new(null, null, null, null, null));
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
        if (mainWindow == null || _settingsDc == null) return;
        using var icon = mainWindow.Env.Icon;
        if (icon == null)
        {
            Log.Error("mainWindow.Env.Icon is null");
            return;
        }

        if (icon.MimeType is not SVGMime)
        {
            icon.Use(_ => { });
            if (icon.MimeType is not SVGMime)
            {
                Log.Error("An instance of mainWindow.Env.Icon's mime is not an SVGMime, it is a {@MimeType}",
                    icon.MimeType);
                return;
            }
        }

        if (IconTemplate == null)
        {
            icon.Use(s =>
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
                Source = SvgSource.LoadFromSvg(realisedTemplate)
            };
            CurrentSource = realisedTemplate;
        }
    }

    private async void PointerReleasedProductIcon(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right && CurrentSource != null && Clipboard != null)
        {
            await Clipboard.SetTextAsync(CurrentSource);
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
        get => _backgroundColour;

        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException("Not a valid colour", nameof(BackgroundColour));
            }

            this.RaiseAndSetIfChanged(ref _backgroundColour, value);
            WindowExtensions.envBackend.SetString(ConstStrings.BG, _backgroundColour);

            Dispatcher.UIThread.Post(
                () => { WindowExtensions.OnStyleChange?.Invoke(this, new(_backgroundColour, null, null, null, null)); },
                DispatcherPriority.Normal);
        }
    }

    public string _progressBarColour = WindowExtensions.envBackend.GetString(ConstStrings.PB) ?? "";

    public string ProgressBarColour
    {
        get => _progressBarColour;

        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException("Not a valid colour", nameof(ProgressBarColour));
            }

            this.RaiseAndSetIfChanged(ref _progressBarColour, value);
            WindowExtensions.envBackend.SetEnv(ConstStrings.PB, _progressBarColour);

            Dispatcher.UIThread.Post(() =>
            {
                settings.mainWindow?.SetProgressBarColour(_progressBarColour.ParseBackground(new LinearGradientBrush()
                    { GradientStops = Settings.defPBStops }));
            }, DispatcherPriority.Normal);
        }
    }

    public string _lyricsColour = WindowExtensions.envBackend.GetString(ConstStrings.Lrc) ?? "";

    public string LyricsColour
    {
        get => _lyricsColour;

        set
        {
            if (value.Contains('@'))
            {
                throw new ArgumentException("Not a valid colour", nameof(LyricsColour));
            }

            this.RaiseAndSetIfChanged(ref _lyricsColour, value);
            WindowExtensions.envBackend.SetEnv(ConstStrings.Lrc, _lyricsColour);

            Dispatcher.UIThread.Post(() =>
            {
                var brush = _lyricsColour.ParseBackground(new SolidColorBrush(Color.FromRgb(230, 255, 0)));

                if (brush is SolidColorBrush scb)
                {
                    brush = new SolidColorBrush(scb.Color, 1);
                }

                LyricsView.NotifyColorChanged(brush);
            }, DispatcherPriority.Normal);
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

    public ObservableCollection<InfoPRecord> Items { get; set; } = [];

    public string ProductName { get; internal set; } = string.Empty;

    public string ProductDescription { get; internal set; } = string.Empty;

    public Config Config { get; set; } = new();

    private IImage? _productIcon;

    public IImage? ProductIcon
    {
        get => _productIcon;
        set => this.RaiseAndSetIfChanged(ref _productIcon, value);
    }
}
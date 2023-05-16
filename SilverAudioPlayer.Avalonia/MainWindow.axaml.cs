using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Swordfish.NET.Collections;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using DynamicData;

namespace SilverAudioPlayer.Avalonia;

public class MainWindowContext : PlayerContext
{
    private readonly MainWindow mainWindow;
    private GradientStops _GradientStops;
    private IBrush _pbForeGround;

    private string _Title;
    public AvaloniaList<Song> queue = new();

    public MainWindowContext(MainWindow mw)
    {
        mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
        Selection = new SelectionModel<Song>
        {
            SingleSelect = false
        };
        GradientStops defPBStops = new()
        {
            new(KnownColor.Coral.ToColor(), 0),
            new(KnownColor.SilverCraftBlue.ToColor(), 1)
        };
        PBForeground = WindowExtensions.envBackend.GetString("SAPPBColor")
            .ParseBackground(new LinearGradientBrush() { GradientStops = defPBStops });
        if (PBForeground is LinearGradientBrush lgb)
        {
            GradientStops = lgb.GradientStops;
        }
        else if (PBForeground is SolidColorBrush scb)
        {
            GradientStops = new GradientStops
            {
                new GradientStop(scb.Color, 0)
            };
        }
        else
        {
            GradientStops = new GradientStops
            {
                new GradientStop(KnownColor.Coral.ToColor(), 0)
            };
        }
    }

    public IBrush PBForeground
    {
        get => _pbForeGround;
        set => this.RaiseAndSetIfChanged(ref _pbForeGround, value);
    }

    public SelectionModel<Song> Selection { get; }

    public string Title
    {
        get => _Title;
        set => this.RaiseAndSetIfChanged(ref _Title, value);
    }

    public GradientStops GradientStops
    {
        get => _GradientStops;
        set => this.RaiseAndSetIfChanged(ref _GradientStops, value);
    }


    public void LyricsView()
    {
        LyricsView i = new(mainWindow);
        i.Show();
    }
}

public partial class MainWindow : Window
{
    public Config config;

    public static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "Configs", "SilverAudioPlayer.Config.xml");

    public readonly MainWindowContext dc;
    public SapAvaloniaPlayerEnviroment Env { get; }
    private bool en;
    private bool en2;
    private MetadataView? metadataView;
    public CommentXmlConfigReaderNotifyWhenChanged<Config> reader;

    private Thread? th;
    private CancellationTokenSource? token = new();

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        // Closing += (s, e) => Player?.Stop();
        PlayButton.Click += PlayButton_Click;
        PauseButton.Click += PauseButton_Click;
        StopButton.Click += StopButton_Click;
        PB = this.FindControl<ProgressBar>("PB");
        LT = this.FindControl<TextBlock>("LT");
        mainListBox = this.FindControl<ListBox>("mainListBox");
        PB.PointerReleased += PB_PointerReleased;
        mainListBox.DoubleTapped += TreeView_DoubleTapped;
        Closing += MainWindow_Closing;
        Opened += MainWindow_Opened;
        Settings.Click += Settings_Click;
        mainListBox.PointerMoved += TreeView_PointerMoved;
        mainListBox.PointerReleased += TreeView_PointerReleased;
        mainListBox.PointerPressed += TreeView_PointerPressed1;
        reader = new CommentXmlConfigReaderNotifyWhenChanged<Config>();
        Env = new SapAvaloniaPlayerEnviroment(this);
        if (!File.Exists(ConfigPath)) reader.Write(new Config(), ConfigPath);
        config = reader.Read(ConfigPath) ?? new Config();
        dc = new MainWindowContext(this)
        {
            SetLoopType = lt =>
            {
                if (config.LoopType != lt)
                {
                    config.LoopType = lt;
                    config._AllowedRead = false;
                    reader.Write(config, ConfigPath);
                    config._AllowedRead = true;
                }

                dc?.RaisePropertyChanged(nameof(dc.LoopType));
            },

            GetLoopType = () => config.LoopType,
            VolumeChanged = vol =>
            {
                config.Volume = vol;
                Player?.SetVolume(vol);
                dc?.RaisePropertyChanged(nameof(dc.Volume));
                if (config.Volume != vol && config._AllowedRead)
                {
                    config._AllowedRead = false;
                    reader.Write(config, ConfigPath);
                    config._AllowedRead = true;
                }
            },
            GetVolume = () => config.Volume,
            ResetUIScrollBar = () =>
            {
                Dispatcher.UIThread.InvokeAsync(() => PB.Value = 0);
                Dispatcher.UIThread.InvokeAsync(() => LT.Text = TimeSpan.Zero.ToString());
                token = new CancellationTokenSource();
                th = new Thread(() => SndUpdThrd(token.Token));
            },
            SetScrollBarTextTo = scrl =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PB.Maximum = scrl.TotalMilliseconds;
                    RT.Text = scrl.ToString();
                });
            },
            HandleLateStageMetadataAndScrollBar = () =>
            {
                th.Start();
                if (CurrentSong?.Metadata != null)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (CurrentSong.Metadata.Title != null)
                        {
                            Title = CurrentSong.TitleOrURL() + " - SilverAudioPlayer";
                        }

                        if (CurrentSong?.Metadata?.Pictures?.Any() == true)
                        {
                            if (Image.Source != null && Logic.SongHistory.TryPeek(out var lastsong))
                            {
                                if (lastsong != null && CurrentSong.Metadata.Pictures.Count != 0 &&
                                    lastsong?.Metadata?.Pictures.Count != 0 && CurrentSong.Metadata.Pictures[0].Hash ==
                                    lastsong?.Metadata?.Pictures[0]?.Hash)
                                {
                                    return;
                                }
                            }

                            var imageData = CurrentSong.Metadata.Pictures[0].Data;
                            if (imageData != null)
                            {
                                try
                                {
                                    using var imageDataStream = imageData.GetStream();
                                    var bmp = new Bitmap(imageDataStream);
                                    Image.Source = bmp;
                                    if (config.DisableAlbumArtBlur)
                                    {
                                        return;
                                    }
                                 
                                    using MemoryStream blur = new();
                                    using var stream = imageData.GetStream();
                                    using var wrbmp = SKBitmap.Decode(stream);
                                    var info = new SKImageInfo(wrbmp.Info.Width, wrbmp.Info.Height);
                                    using (var surface = SKSurface.Create(info))
                                    {
                                        var canvas = surface.Canvas;
                                        using (var paint = new SKPaint())
                                        {
                                            paint.ImageFilter = SKImageFilter.CreateBlur(4, 4);
                                            paint.ColorFilter =
                                                SKColorFilter.CreateColorMatrix(new float[]
                                                {
                                                    1, 0, 0, 0, 0,
                                                    0, 1, 0, 0, 0,
                                                    0, 0, 1, 0, 0,
                                                    0, 0, 0, 0.25f, 0
                                                });
                                            canvas.DrawBitmap(wrbmp, SKPoint.Empty, paint: paint);
                                        }
                                        surface.Snapshot().Encode(SKEncodedImageFormat.Png, 76).AsStream()
                                            .CopyTo(blur);
                                        blur.Position = 0;
                                    }
                                    var imgbrsh = new ImageBrush(new Bitmap(blur))
                                    {
                                        Stretch = Stretch.UniformToFill
                                    };
                                    var s = Background;
                                    Background = imgbrsh;
                                    var x = s as IDisposable;
                                    x?.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    //We have more important things to do than having our app crashed
                                    Log.Error(ex, "Error loading image into main window");
                                }
                            }


                            else
                            {
                                Image.Source = null;
                                if (!config.DisableAlbumArtBlur)
                                {
                                    Background = WindowExtensions.envBackend.GetString("SAPColor")
                                        .ParseBackground(def: SAPAWindowExtensions.defBrush);
                                }
                            }
                        }
                    });
                }
            },
            ShowMessageBox = (s, s1) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var window = new MessageBox(s, s1)
                    {
                        Title = "Error",
                        Icon = Icon
                    };
                    window.DoAfterInitTasksF();
                    window.ShowDialog(this);
                });
            }
        };
        dc.GetQueue = () => dc.queue;
        dc.SetQueue = (q) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (q is AvaloniaList<Song> l)
                {
                    dc.queue = l;
                    dc.RaisePropertyChanged("Queue");
                }
                else
                {
                    var n = new AvaloniaList<Song>(q);
                    dc.queue = n;
                    dc.RaisePropertyChanged("Queue");
                }
            });
        };
        Logic = new Logic<MainWindowContext>(dc)
        {
            ChoosePlayProvider = async (x, m) =>
            {
                if (x.Count() < 2)
                {
                    return x.FirstOrDefault();
                }

                if (preferredplayer != null)
                {
                    var y = x.FirstOrDefault(x => x.GetType() == preferredplayer);
                    if (y != null)
                    {
                        return y;
                    }
                }

                if (config.PreferedPlayers.TryGetValue(m.MimeType.Common, out var v))
                {
                    var y = x.FirstOrDefault(x => x.GetType().FullName == v);
                    if (y != null)
                    {
                        return y;
                    }
                }

                return await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    ChooseProvider w = new();
                    w.SetProviders(x);
                    await w.ShowDialog(this);
                    if (w.SetAsDefaultIfPresent == true)
                    {
                        preferredplayer = w.Selected?.GetType();
                    }

                    if (w.SetAsDefaultForFileType == true)
                    {
                        if (config.PreferedPlayers.ContainsKey(m.MimeType.Common))
                        {
                            config.PreferedPlayers[m.MimeType.Common] = w.Selected?.GetType().FullName;
                        }
                        else
                        {
                            config.PreferedPlayers.Add(m.MimeType.Common, w.Selected?.GetType().FullName);
                        }

                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }

                    return w.Selected;
                });
            }
        };
        config.PropertyChanged += Config_PropertyChanged;
        var ob = this.ObservableForProperty(x => x.Title, skipInitial: false);
        ob.Subscribe(x => dc.Title = x.Value);
        DataContext = dc;
        RepeatButton = this.FindControl<Button>("RepeatButton");
        RepeatButton.Click += RepeatButton_Click;
        TransparencyLevelHint = WindowExtensions.envBackend.GetEnum<WindowTransparencyLevel>("SAPTransparency") ??
                                WindowTransparencyLevel.AcrylicBlur;
        Background = WindowExtensions.envBackend.GetString("SAPColor")
            .ParseBackground(def: SAPAWindowExtensions.defBrush);
    }

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == reader)
        {
            switch (e.PropertyName)
            {
                case "LoopType":
                    dc?.SetLoopType?.Invoke(config.LoopType);
                    break;
                case "Volume":
                    dc?.VolumeChanged?.Invoke(config.Volume);
                    break;
                default:
                    break;
            }
        }
    }

    Type? preferredplayer;

    public Logic<MainWindowContext> Logic { get; set; }

    public IPlay? Player
    {
        get => Logic.Player;
        set => Logic.Player = value;
    }

    public Song? CurrentSong
    {
        get => dc.CurrentSong;
        set => dc.CurrentSong = value;
    }

    private void RepeatButton_Click(object? sender, RoutedEventArgs e)
    {
        switch (dc.LoopType)
        {
            case RepeatState.None:
                dc.LoopType = RepeatState.One;
                break;

            case RepeatState.One:
                dc.LoopType = RepeatState.Queue;
                break;

            case RepeatState.Queue:
                dc.LoopType = RepeatState.None;
                break;
        }
    }

    public void SetPBColor(IBrush c)
    {
        if (c is LinearGradientBrush lgb)
        {
            dc.GradientStops = lgb.GradientStops;
        }
        else if (c is SolidColorBrush scb)
        {
            dc.GradientStops = new GradientStops
            {
                new GradientStop(scb.Color, 0)
            };
        }
        else
        {
            dc.GradientStops = new GradientStops
            {
                new GradientStop(KnownColor.Coral.ToColor(), 0)
            };
        }
    }

    private void TreeView_PointerPressed1(object? sender, PointerPressedEventArgs e)
    {
        en = true;
        en2 = true;
    }

    private void TreeView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (en2)
        {
            en = false;
            en2 = false;
        }
    }

    private void TreeView_PointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(mainListBox);
        var size = mainListBox.Bounds.Size;
        if (en && (pos.X < 0 || pos.Y < 0 || pos.X > size.Width || pos.Y > size.Height))
        {
            var dragData = new DataObject();
            var q = dc.Selection.SelectedItems;
            dragData.Set(DataFormats.FileNames, q.Select(x => x.URI));
            DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Link);
            en = false;
            en2 = false;
        }
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var s = new Settings(this);
        s.Show();
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        Logic.MainWindow_Opened(Env);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        Logic.StopAutoLoading = true;
        Parallel.ForEach(Logic.MusicStatusInterfaces.ToArray(), dangthing => Logic.RemoveMSI(dangthing, Env));
        if (Player != null) Player.TrackEnd -= Logic.OutputDevice_PlaybackStopped;
        Logic.StopAutoLoading = true;
        Player?.Stop();
        Player = null;
        Environment.Exit(0);
    }

    private void TreeView_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (mainListBox.SelectedItem is Song song) Logic.HandleSongChanging(song, CurrentSong == null);
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        RemoveTrack();
    }

    private void LyricsButton_Click(object? sender, RoutedEventArgs e)
    {
        dc.LyricsView();
    }

    public void Metadata_Click(object? sender, PointerPressedEventArgs e)
    {
        if (CurrentSong != null)
        {
            metadataView = new MetadataView();
            metadataView.LoadSong(CurrentSong);
            metadataView.Show();
        }
    }

    private void PB_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var sp = e.GetPosition(PB);
        var a = PB.Minimum + (PB.Maximum - PB.Minimum) * sp.X / PB.Bounds.Width;
        PB.Value = a;
        var at = TimeSpan.FromMilliseconds(a);
        LT.Text = at.ToString();
        Player?.SetPosition(at);
    }

    private void PauseButton_Click(object? x, RoutedEventArgs y)
    {
        Logic.Pause();
    }

    private void PlayButton_Click(object? x, RoutedEventArgs y)
    {
        Logic.Play();
    }


    private void SndUpdThrd(CancellationToken e)
    {
        var exit = false;
        Thread.CurrentThread.Name = "SndUpdThrd";
        while (!(e.IsCancellationRequested && exit))
            if (Player?.GetPlaybackState() == PlaybackState.Playing)
            {
                if (e.IsCancellationRequested) return;
                try
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (IsVisible)
                        {
                            var x = Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2);
                            PB.Value = x.TotalMilliseconds;
                            LT.Text = x.ToString();
                        }

                        exit = PB.Value >= PB.Maximum;
                    }, DispatcherPriority.Layout);
                }
                catch (Exception ex)
                {
                    Logic.log.Error(ex.Message);
                    return;
                }

                Thread.Sleep(70);
            }
            else if (Player?.GetPlaybackState() == PlaybackState.Paused ||
                     Player?.GetPlaybackState() == PlaybackState.Buffering)
            {
                //uses 12% of cpu when paused if removed lmao
                Thread.Sleep(270);
            }
            else
            {
                return;
            }
    }

    public void RemoveTrack()
    {
        Dispatcher.UIThread.InvokeAsync(() => Title = "SilverAudioPlayer");
        Player?.Stop();
        Player = null;
        token?.Cancel();
        Thread.Sleep(30);
    }


    private void DragOver(object sender, DragEventArgs? e)
    {
        if (e == null)
        {
            return;
        }

        if (e.Source is Control c && c.Name == "MoveTarget")
            e.DragEffects &= DragDropEffects.Move;
        else if (e.Data.Contains(DataFormats.Files) || e.Data.Contains("UniformResourceLocatorW"))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void Drop(object sender, DragEventArgs? e)
    {
        if (e == null)
        {
            return;
        }

        if (e.Source is Control c && c.Name == "MoveTarget")
            e.DragEffects &= DragDropEffects.Move;
        else
            e.DragEffects &= DragDropEffects.Copy;
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e!.Data.GetFiles();
            if (files != null)
            {
                Logic.ProcessFiles(files.Select(x=>x.TryGetLocalPath()));
            }
        }

        if (e.Data.Contains("UniformResourceLocatorW"))
        {
            var url = e!.Data!.GetText();
            var psps = Logic.PlayStreamProviders.Where(x =>
                x is IPlayStreamProviderThatSupportsUrls y && y.IsUrlSupported(new(url), Env));
            if (psps.Any())
            {
                Task.Run(async () => await ((IPlayStreamProviderThatSupportsUrls)psps.First()).LoadUrlAsync(new(url), Env));
                return;
            }

            if (!string.IsNullOrEmpty(url))
            {
                Logic.ProcessFiles(new[] { url });
            }
        }
    }

    public void ClearAll(object sender, RoutedEventArgs e)
    {
        var delList = dc.Queue.ToList();
        foreach (var track in delList)
        {
            if (dc.CurrentSong != track)
            {
                track.Dispose();
            }

            dc.Queue.Remove(track);
        }
    }

    public async void AddFilee(object sender, RoutedEventArgs e)
    {
        var af = Logic.PlayableMimes.Select(x => x.Common).ToList();
        af.AddRange(Logic.PlayableMimes.SelectMany(y => y.AlternativeTypes));
        var fileDialogFilters = new List<FilePickerFileType>
        {
            new("Audio Files")
            {
                Patterns = Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0)
                    .SelectMany(x => x.FileExtensions.Select(y => "*" + y)).ToList(),
                MimeTypes = af
            },
            new("Everything else")
                { Patterns = new List<string>() { "*" }, MimeTypes = new List<string>() { "application/octet-stream" } }
        };
        foreach (var mime in Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0))
        {
            fileDialogFilters.Add(new(mime.FileExtensions[0].ToUpper() + " Files")
            {
                MimeTypes = mime.AlternativeTypes.Union(new List<string>() { mime.Common }).ToList(),
                Patterns = mime.FileExtensions.Select(x => "*" + x).ToList()
            });
        }

        var u = config.DialogStartLoc;
        IStorageFolder? musicfolder = null;
        if (!string.IsNullOrEmpty(u))
        {
            if (Enum.TryParse<WellKnownFolder>(u, out var folder))
            {
                musicfolder = await StorageProvider.TryGetWellKnownFolderAsync(folder);
            }
            else
            {
                musicfolder = await StorageProvider.TryGetFolderFromPathAsync(u);
            }
        }

        var a = await StorageProvider.OpenFilePickerAsync(new()
        {
            AllowMultiple = true, Title = "Add a file or files to the queue", FileTypeFilter = fileDialogFilters,
            SuggestedStartLocation = musicfolder
        });
        if (a != null) Logic.ProcessFiles(a.Select(x => x.Path.LocalPath));
    }

    public void RemoveSelected(object sender, RoutedEventArgs e)
    {
        Logic.log.Information("RemoveSelected called");
        while (dc.Selection.SelectedIndexes.Count != 0)
        {
            var selected = dc.Selection.SelectedItem;
            if (selected == Logic.NextSong)
            {
                Logic.log.Information("Selected is nextsong");
                var a = dc.Queue.IndexOf(selected);
                if (dc.Queue.Count > a + 1)
                {
                    Logic.log.Information("NextSong is set the next one");
                    Logic.NextSong = dc.Queue[a + 1];
                }
                else
                {
                    Logic.log.Information("NextSong is set to null");
                    Logic.NextSong = null;
                }
            }

            if (dc.CurrentSong != selected)
            {
                selected.Dispose();
            }

            dc.Queue.Remove(selected);
        }
    }
}
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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SilverAudioPlayer.Shared.SilverPlaylist;
using SilverCraft.Shared;

namespace Catenoid;

public class MainWindowContext : PlayerContext
{
    private readonly MainWindow mainWindow;
    private GradientStops _GradientStops;
    private IBrush _pbForeGround;

    private string _Title;
    public AvaloniaList<Song> queue = [];

    public MainWindowContext(MainWindow mw)
    {
        mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
        Selection = new SelectionModel<Song>
        {
            SingleSelect = false
        };
        GradientStops defPBStops =
        [
            new(KnownColor.Coral.ToColor(), 0),
            new(KnownColor.SilverCraftBlue.ToColor(), 1)
        ];
        _pbForeGround = PBForeground = WindowExtensions.envBackend.GetString("SAPPBColor")
            .ParseBackground(new LinearGradientBrush() { GradientStops = defPBStops });
        _GradientStops = GradientStops = PBForeground switch
        {
            LinearGradientBrush lgb => lgb.GradientStops,
            SolidColorBrush scb => [new GradientStop(scb.Color, 0)],
            _ => [new GradientStop(KnownColor.Coral.ToColor(), 0)]
        };
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
public partial class MainWindow : Window, IWindowSuggestVisualChange
{
    public readonly MainWindowContext dc;
    public IFullPlayerEnvironment Env => Logic.PlayerEnvironment;
    private MetadataView? metadataView;
    private DispatcherTimer? _seekBarTimer;
    private bool AlbumArtTransparencyChanged = false;
    public Config config => App.mwconfig;
    public CommentXmlConfigReaderNotifyWhenChanged<Config> reader => App.mwconfreader;
    private string? CurrentAlbumArtHash = null;
    public MainWindow(IServiceProvider provider)
    {
        _seekBarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _seekBarTimer.Tick += SeekBarUpdate;
        dc = new MainWindowContext(this)
        {
            ServiceProvider = provider,
            SetLoopType = lt =>
            {
                if (config.LoopType != lt)
                {
                    config.LoopType = lt;
                    reader.Write(config, App._configPath);
                }

                dc?.RaisePropertyChanged(nameof(dc.LoopType));
                if (Player is not ILoop loop) return;
                if (dc is { LoopType: RepeatState.One })
                {
                    loop.EnableLoop();
                }
                else
                {
                    loop.DisableLoop();
                }
            },

            GetLoopType = () => config.LoopType,
            VolumeChanged = vol =>
            {
                if (config.Volume != vol)
                {
                    config.Volume = vol;
                }
                Player?.SetVolume(vol);
                dc?.RaisePropertyChanged(nameof(dc.Volume));
            },
            GetVolume = () => config.Volume,
            ResetUIScrollBar = () =>
            {
                Dispatcher.UIThread.InvokeAsync(() => PB.Value = 0);
                Dispatcher.UIThread.InvokeAsync(() => LT.Text = TimeSpan.Zero.ToString());
                _seekBarTimer.Start();
            },
            SetScrollBarTextTo = scrl =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PB.Maximum = scrl.TotalMilliseconds;
                    RT.Text = scrl.ToString(TimestampFormat);
                });
            },
            HandleLateStageMetadataAndScrollBar = () =>
            {
                if (!_seekBarTimer.IsEnabled)
                {
                    _seekBarTimer.Start();
                }
                if (CurrentSong?.Metadata == null) return;
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (CurrentSong.Metadata.Title != null)
                    {
                        Title = CurrentSong.TitleOrURL() + " - Catenoid";
                    }
                    if (CurrentSong?.Metadata?.SyncedLyrics?.Count is not null and not 0)
                    {
                        if (this.TryFindResource("SystemControlForegroundAccentBrush", ActualThemeVariant, out var result2) && result2 is IBrush brush)
                        {
                            LyricsButton.Foreground = brush;
                        }
                        else
                        {
                            Log.Error("Failed to find SystemControlForegroundAccentBrush?");
                        }
                    }
                    else
                    {
                        if (this.TryFindResource("SystemControlForegroundBaseHighBrush", ActualThemeVariant, out var result2) && result2 is IBrush brush)
                        {
                            LyricsButton.Foreground = brush;
                        }
                        else
                        {
                            Log.Error("Failed to find SystemControlForegroundBaseHighBrush?");
                        }
                    }
                });
                HandleSongChangingImage();
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
        dc.SetQueue = q =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (q is AvaloniaList<Song> l)
                {
                    dc.queue = l;
                }
                else
                {
                    var n = new AvaloniaList<Song>(q);
                    dc.queue = n;
                }
                dc.RaisePropertyChanged(nameof(dc.Queue));
            });
        };
        Logic = new Logic<MainWindowContext>(dc)
        {
            CheckIfPluginAllowed = s => !config.BlockedPlugins.Contains(s),
            ChoosePlayProvider = async (x, m) =>
            {
                var playProviders = x.ToList();
                if (playProviders.Count < 2)
                {
                    return playProviders.FirstOrDefault();
                }

                if (!config.PreferredPlayers.TryGetValue(m.MimeType.Common, out var v))
                    return (IPlayProvider?)await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        ChooseProvider w = new();
                        await w.SetProvidersAsync(playProviders);
                        await w.ShowDialog(this);

                        if (w.SetAsDefaultForFileType != true) return w.Selected;
                        config.PreferredPlayers[m.MimeType.Common] = w.Selected?.GetType().FullName;

                        reader.Write(config, App._configPath);
                        return w.Selected;
                    });

                var y = playProviders.FirstOrDefault(playProvider => playProvider.GetType().FullName == v);
                if (y != null)
                {
                    return y;
                }

                return (IPlayProvider?)await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    ChooseProvider w = new();
                    await w.SetProvidersAsync(playProviders);
                    await w.ShowDialog(this);

                    if (w.SetAsDefaultForFileType != true) return w.Selected;
                    config.PreferredPlayers[m.MimeType.Common] = w.Selected?.GetType().FullName;
                    reader.Write(config, App._configPath);
                    return w.Selected;
                });
            },
        };
        Logic.PlayerEnvironment = new SapAvaloniaPlayerEnviroment(Logic,Logic.PlayerContext.ServiceProvider.GetRequiredService<IConfigPathProvider>());
        InitializeComponent();
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }

        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        PlayButton.Click += PlayButton_Click;
        PauseButton.Click += PauseButton_Click;
        StopButton.Click += StopButton_Click;
        PB = this.FindControl<ProgressBar>("PB");
        LT = this.FindControl<TextBlock>("LT");
        mainListBox = this.FindControl<ListBox>("mainListBox");
        PB.PointerReleased += PB_PointerReleased;
        mainListBox.DoubleTapped += TreeView_DoubleTapped;
        mainListBox.KeyUp += TreeView_KeyPressed;
        Closing += MainWindow_Closing;
        Opened += MainWindow_Opened;
        Settings.Click += Settings_Click;
        CommandMenu.Click += CommandMenuClick;
        config.PropertyChanged += Config_PropertyChanged;
        var ob = this.ObservableForProperty(x => x.Title, false);
        ob.Subscribe(x => dc.Title = x.Value);
        DataContext = dc;
        RepeatButton = this.FindControl<Button>("RepeatButton");
        RepeatButton.Click += RepeatButton_Click;
        this.DoAfterInitTasksF(options: new()
        {
            SetBackground = false,
            SetTransparency = true,
            SetFontFamily = true,
        }, followOptionsOnCall: false);
    }

 private void HandleSongChangingImage()
{
    if (CurrentSong?.Metadata?.Pictures?.Any() == true)
    {
        var newPicture = CurrentSong.Metadata.Pictures[0];
        
        var skip = Dispatcher.UIThread.Invoke(() =>
        {
            if (Image.Source != null && newPicture.Hash == CurrentAlbumArtHash && !AlbumArtTransparencyChanged)
                return true;
            AlbumArtTransparencyChanged = false;
            CurrentAlbumArtHash = newPicture.Hash;
            return false;
        });
        
        if (skip) return;
        
        var imageData = newPicture.Data;
        if (imageData == null) return;

        try
        {
            using var imageDataStream = imageData.GetStream();
            var rawImageStream = new MemoryStream();
            imageDataStream.CopyTo(rawImageStream);
            rawImageStream.Position = 0;

            MemoryStream? blurredStream = null;

            if (!config.DisableAlbumArtBlur)
            {
                blurredStream = new MemoryStream();
                using var stream = imageData.GetStream();
                using var writableBitmap = SKBitmap.Decode(stream);
                var info = new SKImageInfo(writableBitmap.Info.Width, writableBitmap.Info.Height);
                using var surface = SKSurface.Create(info);
                
                var canvas = surface.Canvas;
                using var paint = new SKPaint();
                paint.ImageFilter = SKImageFilter.CreateBlur(4, 4);
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(
                [
                    1, 0, 0, 0, 0,
                    0, 1, 0, 0, 0,
                    0, 0, 1, 0, 0,
                    0, 0, 0, config.AlbumArtTransparency, 0
                ]);
                canvas.DrawBitmap(writableBitmap, SKPoint.Empty, paint: paint);

                using var snapshot = surface.Snapshot();
                using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 76);
                using var imgStream = encoded.AsStream();
                imgStream.CopyTo(blurredStream);
                blurredStream.Position = 0;
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                using (rawImageStream)
                {
                    var newBmp = new Bitmap(rawImageStream);
                    var oldSource = Image.Source;
                    Image.Source = newBmp;
                    
                    if (oldSource is IDisposable disposableSource)
                    {
                        disposableSource.Dispose();
                    }
                }

                if (config.DisableAlbumArtBlur || blurredStream == null) return;
                using (blurredStream)
                {
                    var blurBmp = new Bitmap(blurredStream);
                    var newBrush = new ImageBrush(blurBmp)
                    {
                        Stretch = Stretch.UniformToFill
                    };

                    var oldBackground = Background;
                    Background = newBrush;

                    if (oldBackground is IDisposable disposableBackground)
                    {
                        disposableBackground.Dispose();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading image into main window");
        }
    }
    else
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Image.Source = null;
            CurrentAlbumArtHash = null;
            
            if (config.DisableAlbumArtBlur) return;
            
            var oldBackground = Background;
            Background = WindowExtensions.envBackend.GetString("SAPColor").ParseBackground();
            
            if (oldBackground is IDisposable disposableBackground)
            {
                disposableBackground.Dispose();
            }
        });
    }
}


    private bool FirstThemeColrChange = true;
    public void SuggestVisualChange(StylingChangeData data, bool isColorSet)
    {
        if (isColorSet || !FirstThemeColrChange) return;
        if (data.Accent == null) return;
        var transparency= WindowExtensions.envBackend.GetBool("DisableSAPTransparency") == true ? 1 : WindowExtensions.envBackend.GetDouble("BackgroundTransparency") ?? 0.75;
        Background =new SolidColorBrush((Color)data.Accent, transparency);
        FirstThemeColrChange = false;
    }


    public void Startup()
    {
        try
        {
            FindPrevShutdownAndLoad();
        }
        catch (Exception ex)
        {
            Log.Error(ex,"Exception trying to load previous tracks");
        }

    }
    void FindPrevShutdownAndLoad()
    {
        ChangeAutoSaveInterval(config.AutoSaveInterval);
        if (!config.AutoSave || dc.Queue.Count != 0) return;
        var dirpath = Logic.PlayerContext.ServiceProvider.GetRequiredService<IConfigPathProvider>().GetPath(AutoSavePlaylistPath);
        if (!Directory.Exists(dirpath)) return;
        var filepath = Path.Combine(dirpath, AutoSavePlaylistName);
        if (!File.Exists(filepath)) return;
        var pl = Logic.TryReadPlaylist(new WrappedFileStream(filepath));
        if (pl == null) return;
        foreach (var song in pl.Tracks)
        {
            foreach (var source in song.TrackLoadInfos)
            {
                Logic.AddWithURLBad(source.URL, source.Source == "http", false);
                break;
            }
        }

        if (pl.IndexOfActive == null || !(dc.Queue.Count > pl.IndexOfActive)) return;
        Logic.HandleSongChanging(dc.Queue[pl.IndexOfActive.Value]);
        Logic.SetPosition(pl.PositionOfActive ?? 1);
    }


    private void CommandMenuClick(object? sender, RoutedEventArgs e)
    {
        CommandPalette p = new();
        p.Show();
    }
    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != reader) return;
        switch (e.PropertyName)
        {
            case "LoopType":
                dc?.SetLoopType?.Invoke(config.LoopType);
                break;
            case "Volume":
                dc?.VolumeChanged?.Invoke(config.Volume);
                break;
            case "AlbumArtTransparency":
                AlbumArtTransparencyChanged = true;
                break;
            case "AutoSaveInterval":
                ChangeAutoSaveInterval(config.AutoSaveInterval);
                break;
        }
    }

    private Timer? autoSaveTimer=null;
    void ChangeAutoSaveInterval(double s)
    {
        if (s > 1)
        {
            if (autoSaveTimer == null)
            {
                autoSaveTimer = new(AutoSave, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(s));
            }
            else
            {
                autoSaveTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(s));
            }
            return;
        }
        autoSaveTimer?.Dispose();
        autoSaveTimer = null;
    }
    private const string AutoSavePlaylistPath = "Playlists/Autosave/";
    private const string AutoSavePlaylistName = "AutoSave.spl.br";
    private void AutoSave(object? state)
    {
        if (dc?.queue == null) return;
        Log.Information("Autosaving");
        var queue = Logic.GetQueueCopy();
        var dirpath = Logic.PlayerContext.ServiceProvider.GetRequiredService<IConfigPathProvider>().GetPath(AutoSavePlaylistPath);
        Directory.CreateDirectory(dirpath);
        using var s = new FileStream(Path.Combine(dirpath, AutoSavePlaylistName), FileMode.Create);
        SilverPlaylistHelper.SerialiseBrotli(SilverPlaylistHelper.FromQueueCopy(queue, Env), s);
    }

    private readonly DataFormat<string> _queueReorderFormat = DataFormat.CreateStringApplicationFormat("SilverAudioPlayer.QueueReorder");
    public Logic<MainWindowContext> Logic { get; set; }

    public IPlay? Player => Logic.Player;

    public Song? CurrentSong
    {
        get => dc.CurrentSong;
        set => dc.CurrentSong = value;
    }

    private void RepeatButton_Click(object? sender, RoutedEventArgs e)
    {
        dc.LoopType = dc.LoopType switch
        {
            RepeatState.None => RepeatState.One,
            RepeatState.One => RepeatState.Queue,
            RepeatState.Queue => RepeatState.None,
            _ => dc.LoopType
        };
    }

    public void SetProgressBarColour(IBrush c)
    {
        dc.GradientStops = c switch
        {
            LinearGradientBrush lgb => lgb.GradientStops,
            SolidColorBrush scb => [new(scb.Color, 0)],
            _ => [new(KnownColor.Coral.ToColor(), 0)]
        };
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var s = new Settings(this);
        s.Show();
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        Logic.MainWindow_Opened(Env);
        if (!config.SaveLoadLoc) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new(config.Pos[0], config.Pos[1]); //TODO FIX
        Width = config.Size[0];
        Height = config.Size[1];
        WindowState = config.State;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        Logic.StopAutoLoading = true;
        config.PropertyChanged -= Config_PropertyChanged;
        if (config.AutoSave)
        {
           AutoSave(null);
        }
        Parallel.ForEach([.. Logic.MusicStatusInterfaces], musicStatusInterface => Logic.RemoveMsi(musicStatusInterface, Env));
        if (Player != null) Player.TrackEnd -= Logic.OutputDevice_PlaybackStopped;
        Logic.StopAutoLoading = true;
        Player?.Stop();
        if (config.SaveLoadLoc)
        {
            config.Pos = [Position.X, Position.Y];
            config.Size = [Width, Height];
            config.State = WindowState;
            reader.Write(config, App._configPath);
        }
        Environment.Exit(0);
    }

    private void TreeView_DoubleTapped(object? sender, RoutedEventArgs e) => TreeViewPressed();
    private void TreeView_KeyPressed(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            TreeViewPressed();
        }
    }
    void TreeViewPressed()
    {
        if (mainListBox.SelectedItem is Song song)
        {
            Logic.HandleSongChanging(song);
        }
    }
    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        RemoveTrack();
    }

    private async void QueueItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;
        if (control.DataContext is not Song song) return;
        var sourceIndex = dc.Queue.IndexOf(song);
        if (sourceIndex < 0) return;

        mainListBox.SelectedItem = song;

        var dragData = new DataTransfer();
        dragData.Add(DataTransferItem.Create(_queueReorderFormat, sourceIndex.ToString()));
        await DragDrop.DoDragDropAsync(e, dragData, DragDropEffects.Move);
    }

    private void LyricsButton_Click(object? sender, RoutedEventArgs e) => dc.LyricsView();

    public void Metadata_Click(object? sender, PointerPressedEventArgs e)
    {
        if (CurrentSong == null) return;
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsRightButtonPressed)
        {
            //spawn new window with metadata encoded as json
            RawMetadataView rawMetadataView = new(CurrentSong);
            rawMetadataView.Show();
            return;
        }
        metadataView = new MetadataView();
        metadataView.LoadSong(CurrentSong);
        metadataView.Show();
    }

    private void PB_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var sp = e.GetPosition(PB);
        var a = PB.Minimum + (PB.Maximum - PB.Minimum) * sp.X / PB.Bounds.Width;
        PB.Value = a;
        var at = TimeSpan.FromMilliseconds(a);
        LT.Text = at.ToString(TimestampFormat);
        Player?.SetPosition(at);
    }

    private void PauseButton_Click(object? x, RoutedEventArgs y) => Logic.Pause();

    private void PlayButton_Click(object? x, RoutedEventArgs y) => Logic.Play();

    private const string TimestampFormat = @"hh\:mm\:ss\.ff";
    private void SeekBarUpdate(object? sender, EventArgs e)
    {
        try
        {
            if (!IsVisible) return;
            var state = Player?.GetPlaybackState();
            switch (state)
            {
                case PlaybackState.Playing:
                {
                    var x = Player?.GetPosition() ?? TimeSpan.Zero;
                    PB.Value = x.TotalMilliseconds % PB.Maximum;
                    LT.Text = x.ToString(TimestampFormat);
                    if (x.TotalMilliseconds >= PB.Maximum)
                    {
                        _seekBarTimer?.Stop();
                    }
                    break;
                }
                case PlaybackState.Paused or PlaybackState.Buffering:
                    //No need to update the progress bar
                    break;
                default:
                    _seekBarTimer?.Stop();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logic.Log.Error(ex, "Error in SeekBarTimer Tick");
            _seekBarTimer?.Stop();
        }
    }

    public void RemoveTrack()
    {
        Dispatcher.UIThread.InvokeAsync(() => Title = "Catenoid");
        Logic.RemoveTrack();

    }

    private int GetDropTargetIndex(DragEventArgs e)
    {
        var point = e.GetPosition(mainListBox);
        for (var i = 0; i < mainListBox.ItemCount; i++)
        {
            if (mainListBox.ContainerFromIndex(i) is Control container && container.Bounds.Contains(point))
            {
                return i;
            }
        }

        return dc.Queue.Count;
    }

    private void ReorderQueueItem(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= dc.Queue.Count) return;
        targetIndex = Math.Clamp(targetIndex, 0, dc.Queue.Count);
        if (sourceIndex == targetIndex || sourceIndex + 1 == targetIndex) return;

        var song = dc.Queue[sourceIndex];
        Logic.ReorderQueue(sourceIndex, targetIndex);
        mainListBox.SelectedItem = song;
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(_queueReorderFormat))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Source is Control { Name: "MoveTarget" })
            e.DragEffects &= DragDropEffects.Move;
        else if (e.DataTransfer.Formats.Any(x=>x == DataFormat.Text || x == DataFormat.File) || e.DataTransfer.Formats.Any(x=>x.Identifier is "UniformResourceLocatorW" or "text/x-moz-url" or "text/uri-list"))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(_queueReorderFormat))
        {
            var sourceIndexText = e.DataTransfer.TryGetValue(_queueReorderFormat) as string;
            if (sourceIndexText != null && int.TryParse(sourceIndexText, out var sourceIndex))
            {
                var targetIndex = GetDropTargetIndex(e);
                ReorderQueueItem(sourceIndex, targetIndex);
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }
        }

        if (e.Source is Control { Name: "MoveTarget" })
            e.DragEffects &= DragDropEffects.Move;
        else
            e.DragEffects &= DragDropEffects.Copy;
        if (e.DataTransfer.Formats.Any(x=>x == DataFormat.File))
        {
            var files = e!.DataTransfer.TryGetFiles();
            if (files != null)
            {
                Logic.ProcessFiles(files.Select(x => x.TryGetLocalPath()).Where(x => x != null).Cast<string>());
            }
        }

        if (e.DataTransfer.Formats.All(x => x.Identifier != "UniformResourceLocatorW" && x.Identifier is not "text/x-moz-url" and not "text/uri-list"))return;
        {
            var url = e!.DataTransfer!.TryGetText();
            if (url == null && e.DataTransfer.Formats.Any(x => x.Identifier == "text/x-moz-url"))
            {
                url = e!.DataTransfer.TryGetValue(DataFormat.CreateStringPlatformFormat("text/x-moz-url"));
            }
            else if (url == null && e.DataTransfer.Formats.Any(x => x.Identifier == "text/uri-list"))
            {
                url = e!.DataTransfer.TryGetValue(DataFormat.CreateStringPlatformFormat("text/uri-list"));
            }
            if (string.IsNullOrEmpty(url)) return;
            if (url.Contains('\n'))
            {
                // actually multiple urls
                var urls = url.Split('\n');
                Logic.ProcessFiles(urls);
            }
            else
            {
                Logic.ProcessFiles([url]);
            }
        }
    }

    public void ClearAll(object sender, RoutedEventArgs e) => Logic.ClearAll();

    public async void AddFile(object sender, RoutedEventArgs e)
    {
        var (mimeTypes, fileExten) = Logic.SupportedTrackAndPlaylistMimes();
        fileExten = fileExten.Select(x => '*' + x).ToList();
        var fileDialogFilters = new List<FilePickerFileType>
        {
            new("Audio And Playlists")
            {
                Patterns = fileExten,
                MimeTypes = mimeTypes
            },
            new("Everything else")
                { Patterns = ["*"], MimeTypes = ["application/octet-stream"] }
        };

        fileDialogFilters.AddRange(Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0).
            Select(mime => new FilePickerFileType(mime.FileExtensions[0].ToUpper() + " Files") { MimeTypes = mime.AlternativeTypes.Union(new List<string>() { mime.Common }).ToList(), Patterns = mime.FileExtensions.Select(x => "*" + x).ToList() }));

        fileDialogFilters.AddRange(Logic.PlaylistReaders.SelectMany(x => x.SupportedMimes).Distinct().Where(x => x.FileExtensions.Length > 0).
            Select(mime => new FilePickerFileType(mime.FileExtensions[0].ToUpper() + " Files") { MimeTypes = mime.AlternativeTypes.Union(new List<string>() { mime.Common }).ToList(), Patterns = mime.FileExtensions.Select(x => "*" + x).ToList() }));

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
            AllowMultiple = true,
            Title = "Add a file or files to the queue",
            FileTypeFilter = fileDialogFilters,
            SuggestedStartLocation = musicfolder
        });
        if (a != null) Logic.ProcessFiles(a.Select(x => x.Path.LocalPath));
    }

    public void RemoveSelected(object sender, RoutedEventArgs e)
    {
        Logic.RemoveSongs(dc.Selection.SelectedItems);
    }
    private void ShowInFolder(object? sender, RoutedEventArgs e)
    {
        foreach (var selected in dc.Selection.SelectedItems)
        {
            var url = selected?.URI;
            if (!string.IsNullOrEmpty(url))
            {
                EnvironmentExtensions.OpenFolder(url);
            }
        }
    }
    private async void SaveQueue(object? sender, RoutedEventArgs e)
    {
        var a = await StorageProvider.SaveFilePickerAsync(new()
        {
            DefaultExtension = ".spl.br",
            FileTypeChoices =
            [
                new FilePickerFileType("Brotli compressed SilverAudioPlayer playlist file (.spl.br)")
                {
                    MimeTypes = [ "application/silverplaylist+br" ],
                    Patterns = ["*.spl.br"]
                }
            ]
        });
        if (a == null) return;
        var queue = Logic.GetQueueCopy();
        SilverPlaylistHelper.SerialiseBrotli(SilverPlaylistHelper.FromQueueCopy(queue, Env), await a.OpenWriteAsync());
    }

    private void QueueManagement(object? sender, RoutedEventArgs e)
    {
        PlaylistWindow playlistWindow = new(this);
        playlistWindow.Show();
    }
}

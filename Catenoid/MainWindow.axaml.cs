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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
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

public partial class MainWindow : Window
{


    public readonly MainWindowContext dc;
    public SapAvaloniaPlayerEnviroment Env { get; }
    private bool en;
    private bool en2;
    private MetadataView? metadataView;

    private Thread? th;
    private CancellationTokenSource? token = new();
    private bool AlbumArtTransparencyChanged = false;
    public Config config => App.mwconfig;
    public CommentXmlConfigReaderNotifyWhenChanged<Config> reader => App.mwconfreader;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
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
        Closing += MainWindow_Closing;
        Opened += MainWindow_Opened;
        Settings.Click += Settings_Click;
        CommandMenu.Click += CommandMenuClick;

        mainListBox.PointerMoved += TreeView_PointerMoved;
        mainListBox.PointerReleased += TreeView_PointerReleased;
        mainListBox.PointerPressed += TreeView_PointerPressed1;
        Env = new SapAvaloniaPlayerEnviroment(this);

        dc = new MainWindowContext(this)
        {
            SetLoopType = lt =>
            {
                if (config.LoopType != lt)
                {
                    config.LoopType = lt;
                    reader.Write(config, App._configPath);
                }

                dc?.RaisePropertyChanged(nameof(dc.LoopType));
                if (Player != null && Player is ILoop loop)
                {
                    if (dc.LoopType == RepeatState.One)
                    {
                        loop.EnableLoop();
                    }
                    else
                    {
                        loop.DisableLoop();
                    }
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
                token = new CancellationTokenSource();
                th = new Thread(() => SeekBarUpdateThread(token.Token));
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
                            Title = CurrentSong.TitleOrURL() + " - Catenoid";
                        }
                        if (CurrentSong?.Metadata?.SyncedLyrics?.Count is not null and not 0)
                        {
                            if (this.TryFindResource("SystemControlForegroundAccentBrush", ActualThemeVariant, out var result2))
                            {
                                LyricsButton.Foreground = (IBrush)result2;
                            }
                            else
                            {
                                Log.Error("Failed to find SystemControlForegroundAccentBrush?");
                            }
                        }
                        else
                        {
                            if (this.TryFindResource("SystemControlForegroundBaseHighBrush", ActualThemeVariant, out var result2))
                            {
                                LyricsButton.Foreground = (IBrush)result2;
                            }
                            else
                            {
                                Log.Error("Failed to find SystemControlForegroundBaseHighBrush?");
                            }
                        }
                        if (CurrentSong?.Metadata?.Pictures?.Any() == true)
                        {
                            if (Image.Source != null && Logic.SongHistory.TryPeek(out var lastsong) &&
                                !AlbumArtTransparencyChanged)
                            {
                                if (lastsong != null && CurrentSong.Metadata.Pictures.Count != 0 &&
                                    lastsong?.Metadata?.Pictures?.Count != 0 && CurrentSong.Metadata.Pictures[0].Hash ==
                                    lastsong?.Metadata?.Pictures[0]?.Hash)
                                {
                                    return;
                                }
                            }

                            AlbumArtTransparencyChanged = false;
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
                                                SKColorFilter.CreateColorMatrix(
                                                [
                                                    1, 0, 0, 0, 0,
                                                0, 1, 0, 0, 0,
                                                0, 0, 1, 0, 0,
                                                0, 0, 0, config.AlbumArtTransparency, 0
                                                ]);
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
                                    Log.Error(ex, "Error loading image into main window");
                                }
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
        dc.SetQueue = q =>
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
                if (config.PreferredPlayers.TryGetValue(m.MimeType.Common, out var v))
                {
                    var y = x.FirstOrDefault(x => x.GetType().FullName == v);
                    if (y != null)
                    {
                        return y;
                    }
                }
                return (IPlayProvider?)await Dispatcher.UIThread.InvokeAsync(async () =>
                     {
                         ChooseProvider w = new();
                         w.SetProviders(x);
                         await w.ShowDialog(this);

                         if (w.SetAsDefaultForFileType != true) return w.Selected;
                         config.PreferredPlayers[m.MimeType.Common] = w.Selected?.GetType().FullName;

                         reader.Write(config, App._configPath);
                         return w.Selected;
                     });
            },
            PlayerEnvironment = Env
        };
        config.PropertyChanged += Config_PropertyChanged;
        var ob = this.ObservableForProperty(x => x.Title, skipInitial: false);
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
    public void Startup()
    {
        FindPrevShutdownAndLoad();
    }
    void FindPrevShutdownAndLoad()
    {
        if (config.AutoSave && dc.Queue.Count == 0)
        {
            var dirpath = ConfigPath.GetPath("Playlists/Autosave/");
            if (Directory.Exists(dirpath))
            {
                var filepath = Path.Combine(dirpath, "AutoSave.spl.br");
                if (File.Exists(filepath))
                {
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
                    if (pl.IndexOfActive != null && dc.Queue.Count > pl.IndexOfActive)
                    {
                        Logic.HandleSongChanging(dc.Queue[pl.IndexOfActive.Value], true);
                        Logic.SetPosition(pl.PositionOfActive ?? 1);
                    }
                }
            }

        }
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
        }
    }


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

    private void TreeView_PointerPressed1(object? sender, PointerPressedEventArgs e)
    {
        en = true;
        en2 = true;
    }

    private void TreeView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!en2) return;
        en = false;
        en2 = false;
    }

    private void TreeView_PointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(mainListBox);
        var size = mainListBox.Bounds.Size;
        if (!en || (!(pos.X < 0) && !(pos.Y < 0) && !(pos.X > size.Width) && !(pos.Y > size.Height))) return;
        var dragData = new DataObject();
        var q = dc.Selection.SelectedItems;
        dragData.Set(DataFormats.Files, q.Select(x => x.URI));
        DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Link);
        en = false;
        en2 = false;
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var s = new Settings(this);
        s.Show();
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        Logic.MainWindow_Opened(Env);
        if (config.SaveLoadLoc)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new(config.Pos[0], config.Pos[1]); //TODO FIX
            Width = config.Size[0];
            Height = config.Size[1];
            WindowState = config.State;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        Logic.StopAutoLoading = true;
        if (config.AutoSave)
        {
            var queue = Logic.GetQueueCopy();
            var dirpath = ConfigPath.GetPath("Playlists/Autosave/");
            Directory.CreateDirectory(dirpath);
            using var s = new FileStream(Path.Combine(dirpath, "AutoSave.spl.br"), FileMode.Create);
            SilverPlaylistHelper.SerialiseBrotli(SilverPlaylistHelper.FromQueueCopy(queue, Env), s);
        }
        Parallel.ForEach(Logic.MusicStatusInterfaces.ToArray(), dangthing => Logic.RemoveMsi(dangthing, Env));
        if (Player != null) Player.TrackEnd -= Logic.OutputDevice_PlaybackStopped;
        Logic.StopAutoLoading = true;
        Player?.Stop();
        Player = null;
        if (config.SaveLoadLoc)
        {
            config.Pos = [Position.X, Position.Y];
            config.Size = [Width, Height];
            config.State = WindowState;
            reader.Write(config, App._configPath);
        }
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
        if (CurrentSong == null) return;
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
        LT.Text = at.ToString();
        Player?.SetPosition(at);
    }

    private void PauseButton_Click(object? x, RoutedEventArgs y) => Logic.Pause();

    private void PlayButton_Click(object? x, RoutedEventArgs y) => Logic.Play();


    private void SeekBarUpdateThread(CancellationToken e)
    {
        var exit = false;
        Thread.CurrentThread.Name = "SeekBarUpdateThread";
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
                            PB.Value = x.TotalMilliseconds % PB.Maximum;
                            LT.Text = x.ToString();
                        }

                        exit = PB.Value >= PB.Maximum;
                    }, DispatcherPriority.Normal);
                }
                catch (Exception ex)
                {
                    Logic.Log.Error(ex.Message);
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
        Dispatcher.UIThread.InvokeAsync(() => Title = "Catenoid");
        Logic.RemoveTrack();
        token?.Cancel();
    }


    private void DragOver(object? sender, DragEventArgs e)
    {
        if (e == null)
        {
            return;
        }

        if (e.Source is Control { Name: "MoveTarget" })
            e.DragEffects &= DragDropEffects.Move;
        else if (e.Data.Contains(DataFormats.Files) || e.Data.Contains("UniformResourceLocatorW"))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e == null)
        {
            return;
        }

        if (e.Source is Control { Name: "MoveTarget" })
            e.DragEffects &= DragDropEffects.Move;
        else
            e.DragEffects &= DragDropEffects.Copy;
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e!.Data.GetFiles();
            if (files != null)
            {
                Logic.ProcessFiles(files.Select(x => x.TryGetLocalPath()).Where(x => x != null).Cast<string>());
            }
        }

        if (!e.Data.Contains("UniformResourceLocatorW")) return;
        {
            var url = e!.Data!.GetText();


            if (!string.IsNullOrEmpty(url))
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
        Logic.Log.Information("RemoveSelected called");
        while (dc.Selection.SelectedIndexes.Count != 0)
        {
            var selected = dc.Selection.SelectedItem;
            if (selected == null)
                return;
            if (selected.Equals(Logic.NextSong))
            {
                Logic.Log.Information("Selected is nextsong");
                var a = dc.Queue.IndexOf(selected);
                if (dc.Queue.Count > a + 1)
                {
                    Logic.Log.Information("NextSong is set the next one");
                    Logic.NextSong = dc.Queue[a + 1];
                }
                else
                {
                    Logic.Log.Information("NextSong is set to null");
                    Logic.NextSong = null;
                }
            }

            if (dc.CurrentSong?.Equals(selected) != true)
            {
                selected.Dispose();
            }

            dc.Queue.Remove(selected);
        }
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

}
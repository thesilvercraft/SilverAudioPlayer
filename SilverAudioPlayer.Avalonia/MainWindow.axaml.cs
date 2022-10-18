using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverConfig.CobaltExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    public class MainWindowContext : PlayerContext
    {
        public MainWindowContext(MainWindow mw)
        {
            mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
        }
        readonly MainWindow mainWindow;

        private string _Title;
        private IBrush _pbForeGround = new SolidColorBrush(WindowExtensions.ReadColor("SAPPBColor"));
        public IBrush PBForeground { get => _pbForeGround; set => this.RaiseAndSetIfChanged(ref _pbForeGround, value); }
        public string Title { get => _Title; set => this.RaiseAndSetIfChanged(ref _Title, value); }

        public void RunTheThing()
        {
            Info i = new(mainWindow);
            i.Show();
        }
        public void LyricsView()
        {
            LyricsView i = new(mainWindow);
            i.Show();
        }
    }

    public partial class MainWindow : Window
    {
        public string ConfigPath = Path.Combine(AppContext.BaseDirectory, "SilverAudioPlayer.Config.xml");
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.DoAfterInitTasks(true);
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
            reader = new();
            if (!File.Exists(ConfigPath))
            {
                reader.Write(new(), ConfigPath);
            }
            config = reader.Read(ConfigPath) ?? new();
            dc = new MainWindowContext(this)
            {
                SetLoopType = (lt) =>
                {
                    if (config.LoopType != lt)
                    {
                        dc?.RaiseAndSetIfChanged(ref dc._LoopType, lt);
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }
                },
                VolumeChanged = (vol) =>
                {
                    config.Volume = vol;
                    if (config._AllowedRead)
                    {
                        config._AllowedRead = false;
                        reader.Write(config, ConfigPath);
                        config._AllowedRead = true;
                    }
                },
                ResetUIScrollBar = () =>
                {
                    Dispatcher.UIThread.InvokeAsync(() => PB.Value = 0);
                    Dispatcher.UIThread.InvokeAsync(() => LT.Text = TimeSpan.Zero.ToString());
                    token = new();
                    th = new Thread(() => SndThrd(token.Token));
                },
                SetScrollBarTextTo = (scrl) =>
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
                        if (CurrentSong.Metadata.Title != null)
                        {
                            Dispatcher.UIThread.InvokeAsync(() => Title = CurrentSong.TitleOrURL() + " - SilverAudioPlayer");
                        }
                        if (CurrentSong?.Metadata?.Pictures?.Any() == true)
                        {
                            var buffer = CurrentSong.Metadata.Pictures[0].Data;
                            if (buffer != null)
                            {
                                try
                                {
                                    var memstream = new MemoryStream(buffer);
                                    Dispatcher.UIThread.InvokeAsync(() => Image.Source = new Bitmap(memstream));
                                }
                                catch (Exception ex)
                                {
                                    //We have more important things to do than having our app crashed
                                    Log.Error(ex, "Error loading image into main window");
                                }
                            }
                        }
                        else
                        {
                            Dispatcher.UIThread.InvokeAsync(() => Image.Source = null);
                        }
                    }
                }
            };
            Logic = new(dc);
            var ob = this.ObservableForProperty(x => x.Title, skipInitial: false);
            ob.Subscribe(x => dc.Title = x.Value);
            DataContext = dc;
            //var ob3 = config.ObservableForProperty(x => x.Volume, skipInitial: true);
            // ob3.Subscribe(x => { Player?.SetVolume(x.GetValue()); dc.RaisePropertyChanged(nameof(dc.V)); });
            //TODO provider selection

            ShowAppInfo = this.FindControl<MenuItem>("ShowAppInfo");
            RepeatButton = this.FindControl<Button>("RepeatButton");
            RepeatButton.Click += RepeatButton_Click;
        }
        public Config config;
        public CommentXmlConfigReaderNotifyWhenChanged<Config> reader;
        MainWindowContext dc;
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

        public void SetPBColor(Color c)
        {
            ((MainWindowContext)DataContext).PBForeground = new SolidColorBrush(c);
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
                var q = from i in mainListBox.SelectedItems.OfType<Song>()
                        select i;
                dragData.Set(DataFormats.FileNames, q.Select(x => x.URI));
                DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Link);
                en = false;
                en2 = false;
            }
        }

        private bool en = false;
        private bool en2 = false;

        public Logic<MainWindowContext> Logic { get; set; }
        public IPlay? Player { get => Logic.Player; set => Logic.Player = value; }
        public Song? CurrentSong { get => dc.CurrentSong; set => dc.CurrentSong = value; }

        private Thread? th;
        private CancellationTokenSource? token = new();

        private void Settings_Click(object? sender, RoutedEventArgs e)
        {
            var s = new Settings
            {
                mainWindow = this
            };
            s.Show();
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            if (Logic.MusicStatusInterfaces?.Any() == true)
            {
                Parallel.ForEach(Logic.MusicStatusInterfaces, dangthing =>
                {
                    var a = dangthing;
                    GC.KeepAlive(a);
                    Logic.AddMSI(a);
                });
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Logic.StopAutoLoading = true;
            Parallel.ForEach(Logic.MusicStatusInterfaces.ToArray(), dangthing => Logic.RemoveMSI(dangthing));
            if (Player != null)
            {
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
            }
            Logic.StopAutoLoading = true;
            Player?.Stop();
            Player = null;
            Environment.Exit(0);
        }

        private void TreeView_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (mainListBox.SelectedItem is Song song)
            {
                Logic.HandleSongChanging(song, CurrentSong == null);
            }
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            RemoveTrack();
        }
        MetadataView? metadataView = null;

        public void Metadata_Click(object? sender, PointerPressedEventArgs e)
        {
            if (CurrentSong != null)
            {
                //metadataView?.Close();
                metadataView = new();
                metadataView.LoadSong(CurrentSong);
                metadataView.Show();
            }
        }

        private void PB_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var sp = e.GetPosition(PB);
            var a = PB.Minimum + ((PB.Maximum - PB.Minimum) * sp.X / PB.Bounds.Width);
            PB.Value = a;
            var at = TimeSpan.FromMilliseconds(a);
            LT.Text = at.ToString();
            Player?.SetPosition(at);
        }

        private void PauseButton_Click(object? sender, RoutedEventArgs e)
        {
            Pause();
        }

        private void PlayButton_Click(object? sender, RoutedEventArgs e)
        {
            Play();
        }

        private void Play()
        {
            Logic.Play();
        }

        private void Pause()
        {
            Logic.Pause();
        }

        private void SndThrd(CancellationToken e)
        {
            bool exit = false;

            while (!(e.IsCancellationRequested && exit))
            {
                if (Player?.GetPlaybackState() == PlaybackState.Playing)
                {
                    if (e.IsCancellationRequested)
                    {
                        return;
                    }
                    try
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (IsVisible)
                            {
                                var x = (Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2));
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
                else if (Player?.GetPlaybackState() == PlaybackState.Paused)
                {
                    //uses 12% of cpu when paused if removed lmao
                    Thread.Sleep(270);
                }
                else
                {
                    return;
                }
            }
        }
        [TimingAdvice]
        public void RemoveTrack()
        {
            Dispatcher.UIThread.InvokeAsync(() => Title = "SilverAudioPlayer");
            Player?.Stop();
            Player = null;
            token?.Cancel();
            Thread.Sleep(30);
        }

        [TimingAdvice]
        private void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Logic.log.Information(@"Output device playback stopped
StopAutoLoading: {StopAutoLoading}
Current song: {CurrentSong}
Loop mode: {LoopType}", Logic.StopAutoLoading, CurrentSong, dc.LoopType);

            if (dc.LoopType == RepeatState.One && !Logic.StopAutoLoading)
            {
                //Loop
                Logic.StartPlaying();
            }
            else if (CurrentSong != null && !Logic.StopAutoLoading)
            {
                var a = dc.Queue.IndexOf(CurrentSong);
                if (a != -1)
                {
                    if (a + 1 < dc.Queue.Count)
                    {
                        Logic.HandleSongChanging(dc.Queue[a + 1], true);
                    }
                    else if (dc.LoopType == RepeatState.Queue)
                    {
                        Logic.HandleSongChanging(dc.Queue[0], true);
                    }
                    else
                    {
                        Logic.log.Information("RemoveTrack in PlaybackStopped");
                        RemoveTrack();
                    }
                }
                else if (Logic.NextSong != null)
                {
                    Logic.HandleSongChanging(Logic.NextSong, true);
                    Logic.NextSong = null;
                }
            }
            Logic.StopAutoLoading = false;
        }

        private void DragOver(object sender, DragEventArgs e)
        {
            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects &= (DragDropEffects.Move);
            }
            else if (e.Data.Contains(DataFormats.FileNames) || e.Data.Contains("UniformResourceLocatorW"))
            {
                e.DragEffects = (DragDropEffects.Copy);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private void Drop(object sender, DragEventArgs e)
        {
            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects &= (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects &= (DragDropEffects.Copy);
            }
            if (e.Data.Contains(DataFormats.FileNames))
            {
                Logic.ProcessFiles(e!.Data.GetFileNames());
            }
            if (e.Data.Contains("UniformResourceLocatorW"))
            {
                Logic.ProcessFiles(new[] { e!.Data.GetText() });
            }
        }

        public void ClearAll(object sender, RoutedEventArgs e)
        {
            dc.Queue.Clear();
        }

        public async void AddFilee(object sender, RoutedEventArgs e)
        {
            List<FileDialogFilter> fileDialogFilters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "Audio Files", Extensions = Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0).SelectMany(x => x.FileExtensions.Select(y => y.TrimStart('.'))).ToList() }, new FileDialogFilter() { Name = "Everything else", Extensions = { "*" } } };
            foreach (var mime in Logic.PlayableMimes.Where(x => x.FileExtensions.Length > 0))
            {
                fileDialogFilters.Add(new() { Name = mime.FileExtensions[0].ToUpper() + " Files", Extensions = mime.FileExtensions.Select(y => y.TrimStart('.')).ToList() });
            }
            OpenFileDialog fd = new()
            {
                Title = "Add file or files to the queue",
                AllowMultiple = true,
                Filters = fileDialogFilters
            };
            var a = await fd.ShowAsync(this);
            if (a != null)
            {
                Logic.ProcessFiles(a);
            }
        }

        public void RemoveSelected(object sender, RoutedEventArgs e)
        {
            Logic.log.Information("RemoveSelected called");
            while (mainListBox.SelectedItems.Count != 0)
            {
                Song selected = (Song)mainListBox.SelectedItems[0];
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
                dc.Queue.Remove(selected);
            }
        }

    }
}
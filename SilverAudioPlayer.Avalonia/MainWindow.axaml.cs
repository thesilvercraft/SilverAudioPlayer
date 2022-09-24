using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverConfig.CobaltExtensions;
using SilverCraft.AvaloniaUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Avalonia
{
    internal class MainWindowContext : ReactiveObject
    {
        internal MainWindowContext(MainWindow mw)
        {
            mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
        }

        readonly MainWindow mainWindow;
        public Action<byte> VolumeChanged;
        public Func<byte> GetVolume;

        private string _Title;
        private IBrush _pbForeGround = new SolidColorBrush(WindowExtensions.ReadColor("SAPPBColor"));
        public IBrush PBForeground { get => _pbForeGround; set => this.RaiseAndSetIfChanged(ref _pbForeGround, value); }
        public string Title { get => _Title; set => this.RaiseAndSetIfChanged(ref _Title, value); }

        public byte V { get => GetVolume(); set => VolumeChanged(value); }
        

        public RepeatState LoopType { get => mainWindow.config.LoopType; set => SetLoopType(value); }
        private void SetLoopType(RepeatState v)
        {
            if (mainWindow.config.LoopType != v)
            {
                mainWindow.config.LoopType = v;
                this.RaisePropertyChanged(nameof(LoopType));
                mainWindow.config._AllowedRead = false;
                mainWindow.reader.Write(mainWindow.config, mainWindow.ConfigPath);
                mainWindow.config._AllowedRead = true;
            }
        }
        public void RunTheThing()
        {
            Info i = new(mainWindow);
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
            Songs = new();
            mainListBox.Items = Songs;
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            Closing += (s, e) => Player?.Stop();
            PlayButton.Click += PlayButton_Click;
            PauseButton.Click += PauseButton_Click;
            StopButton.Click += StopButton_Click;
            PB = this.FindControl<ProgressBar>("PB");
            LT = this.FindControl<TextBlock>("LT");
            PB.PointerReleased += PB_PointerReleased;
            mainListBox.DoubleTapped += TreeView_DoubleTapped;
            Closing += MainWindow_Closing;
            Opened += MainWindow_Opened;
            Settings.Click += Settings_Click;
            mainListBox.PointerMoved += TreeView_PointerMoved;
            mainListBox.PointerReleased += TreeView_PointerReleased;
            mainListBox.PointerPressed += TreeView_PointerPressed1;
            reader = new();
            if(!File.Exists(ConfigPath))
            {
                reader.Write(new(), ConfigPath);
            }
            config = reader.Read(ConfigPath)??new();
            dc = new MainWindowContext(this)
            {
                VolumeChanged = (byte vol) =>
                {
                    Player?.SetVolume(vol);
                    Volume = vol;
                },
                GetVolume = () => Volume,
            };
            var ob = this.ObservableForProperty(x => x.Title, skipInitial: false);
            ob.Subscribe(x => dc.Title = x.Value);
            DataContext = dc;
            var ob3 = config.ObservableForProperty(x => x.Volume, skipInitial: true);
            ob3.Subscribe(x => { Player?.SetVolume(x.GetValue()); dc.RaisePropertyChanged(nameof(dc.V)); });
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
            switch (LoopType)
            {
                case RepeatState.None:
                    LoopType = RepeatState.One;
                    break;
                case RepeatState.One:
                    LoopType = RepeatState.Queue;
                    break;
                case RepeatState.Queue:
                    LoopType = RepeatState.None;
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

        private byte Volume { get => config.Volume; set => SetVolume(value); }
        private void SetVolume(byte v)
        {
            config.Volume = v;
            config._AllowedRead = false;
            reader.Write(config, ConfigPath);
            config._AllowedRead = true;

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

        private ObservableCollection<Song> Songs;
        public Logic Logic { get; set; } = new();
        public IPlay? Player { get; private set; }
        private Song? CurrentSong = null;
        private bool StopAutoLoading = false;
        private Song? NextSong = null;
        private Thread? th;
        private CancellationTokenSource? token = new();
        private readonly List<IMusicStatusInterface> musicStatusInterfaces = new();

        private void AddMSI(IMusicStatusInterface e)
        {
            musicStatusInterfaces.Add(e);
            e.Play += MusicStatusInterface_Play;
            e.Pause += MusicStatusInterface_Pause;
            e.Stop += MusicStatusInterface_Stop;
            e.PlayPause += MusicStatusInterface_PlayPause;
            e.Next += MusicStatusInterface_Next;
            e.Previous += MusicStatusInterface_Previous;
            e.GetCurrentTrack += MusicStatusInterface_GetCurrentTrack;
            e.GetDuration += MusicStatusInterface_GetDuration;
            e.GetPosition += MusicStatusInterface_GetPosition;
            e.SetPosition += MusicStatusInterface_SetPosition;
            e.GetShuffle += MusicStatusInterface_GetShuffle;
            e.GetState += MusicStatusInterface_GetState;
            e.GetVolume += MusicStatusInterface_GetVolume;
            e.SetVolume += MusicStatusInterface_SetVolume;
            e.GetRepeat += MusicStatusInterface_GetRepeat;
            e.SetRating += MusicStatusInterface_SetRating;
            e.SetRepeat += MusicStatusInterface_SetRepeat;
            e.StartIPC();
        }

        private void MusicStatusInterface_SetRepeat(object? sender, RepeatState e)
        {
            LoopType = e;
        }

        private void MusicStatusInterface_SetVolume(object? sender, byte e)
        {
            if (e <= 100)
            {
                Player?.SetVolume(e);
            }
        }

        private void MusicStatusInterface_SetPosition(object? sender, ulong e)
        {
            Player?.SetPosition(TimeSpan.FromSeconds(e));
        }

        private void MusicStatusInterface_SetRating(object? sender, byte e)
        {
            //TODO eventually
        }

        private bool MusicStatusInterface_GetShuffle()
        {
            return false;
        }

        private ulong MusicStatusInterface_GetPosition()
        {
            return (ulong)(Player?.GetPosition().TotalSeconds ?? 1);
        }

        private RepeatState MusicStatusInterface_GetRepeat()
        {
            return LoopType;
        }

        private byte MusicStatusInterface_GetVolume()
        {
            return Volume;
        }

        private PlaybackState MusicStatusInterface_GetState()
        {
            return Player?.GetPlaybackState() ?? PlaybackState.Stopped;
        }

        private void MusicStatusInterface_Stop(object? sender, object e)
        {
            StopAutoLoading = true;
            Player?.Stop();
            SendIfStateIsNotNull();
        }

        private void MusicStatusInterface_Pause(object? sender, object e)
        {
            Player?.Pause();
            SendIfStateIsNotNull();
        }

        private void SendIfStateIsNotNull()
        {
            var state = Player?.GetPlaybackState();
            if (state != null)
            {
                PlaybackStateChangedNotification(state.Value);
            }
        }

        private void TrackChangedNotification(Song? currentSong)
        {
            foreach (var msI in musicStatusInterfaces)
            {
                msI.TrackChangedNotification(currentSong!);
            }
        }

        private void PlaybackStateChangedNotification(PlaybackState s)
        {
            foreach (var msI in musicStatusInterfaces)
            {
                msI.PlayerStateChanged(s);
            }
        }

        private void MusicStatusInterface_Play(object? sender, object e)
        {
            Play();
        }

        private void RemoveMSI(IMusicStatusInterface e)
        {
            e.Play -= MusicStatusInterface_Play;
            e.Pause -= MusicStatusInterface_Pause;
            e.Stop -= MusicStatusInterface_Stop;
            e.Next -= MusicStatusInterface_Next;
            e.Previous -= MusicStatusInterface_Previous;
            e.GetCurrentTrack -= MusicStatusInterface_GetCurrentTrack;
            e.GetDuration -= MusicStatusInterface_GetDuration;
            e.GetState -= MusicStatusInterface_GetState;
            e.GetVolume -= MusicStatusInterface_GetVolume;
            e.GetRepeat -= MusicStatusInterface_GetRepeat;
            e.GetPosition -= MusicStatusInterface_GetPosition;
            e.GetShuffle -= MusicStatusInterface_GetShuffle;
            e.SetRating -= MusicStatusInterface_SetRating;
            e.SetPosition -= MusicStatusInterface_SetPosition;
            e.SetVolume -= MusicStatusInterface_SetVolume;
            e.PlayPause -= MusicStatusInterface_PlayPause;
            e.SetRepeat -= MusicStatusInterface_SetRepeat;

            e.StopIPC();
            e.Dispose();
            musicStatusInterfaces.Remove(e);
        }

        private void PlayPause(bool allowstart)
        {
            if (Player?.GetPlaybackState() == PlaybackState.Playing)
            {
                Pause();
            }
            else if (Player?.GetPlaybackState() == PlaybackState.Paused)
            {
                Play();
            }
            else if (allowstart)
            {
                Debug.WriteLine("Allowstart is true");
                Play();
            }
        }

        private void MusicStatusInterface_PlayPause(object? sender, object e)
        {
            PlayPause(true);
        }

        private ulong MusicStatusInterface_GetDuration()
        {
            return (ulong?)(CurrentSong?.Metadata?.Duration / 1000) ?? 2;
        }

        private Song MusicStatusInterface_GetCurrentTrack()
        {
            return CurrentSong;
        }

        private void MusicStatusInterface_Previous(object? sender, EventArgs e)
        {
            Previous();
        }

        private void MusicStatusInterface_Next(object? sender, EventArgs e)
        {
            Next();
        }

        private void Next()
        {
            var a = Songs.IndexOf(CurrentSong);
            if (a != -1 && a + 1 < Songs.Count)
            {
                HandleSongChanging(Songs[a + 1], true);
            }
        }

        private void Previous()
        {
            var a = Songs.IndexOf(CurrentSong);
            if (a != -1 && a - 1 >= 0)
            {
                HandleSongChanging(Songs[a - 1], true);
            }
        }

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
                    AddMSI(a);
                });
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAutoLoading = true;
            Parallel.ForEach(Logic.MusicStatusInterfaces.ToArray(), dangthing => RemoveMSI(dangthing));
            if (Player != null)
            {
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
            }
            StopAutoLoading = true;
            Player?.Stop();
            Player = null;
            Environment.Exit(0);
        }

        private void TreeView_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (mainListBox.SelectedItem is Song song)
            {
                HandleSongChanging(song, CurrentSong == null);
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
                metadataView?.Close();
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
            if (Player != null)
            {
                Player?.Play();
                SendIfStateIsNotNull();
            }
            else
            {
                StartPlaying();
            }
        }

        private void Pause()
        {
            Player?.Pause();
            SendIfStateIsNotNull();
        }

        public void StartPlaying(bool play = true, bool resetsal = false)
        {
            if (CurrentSong == null)
            {
                if (Songs.Count > 0)
                {
                    CurrentSong = Songs[0];
                }
                else
                {
                    Logic.log.Information("Avoiding fatal crash, nothing to play");
                    return;
                }
            }
            Player = Logic.GetPlayerFromStream(CurrentSong.Stream);

            if (Player == null)
            {
                var window = new MessageBox("Error", "I do not know how to play " + CurrentSong.URI);
                window.ShowDialog(this);
                return;
            }

            Logic.log.Information("Got player of type {PlayerType}",Player.GetType());

            Task.Run(() => TrackChangedNotification(CurrentSong));
            if (play)
            {
                Player.SetVolume(Volume);
                Player.Play();
                SendIfStateIsNotNull();

                Player.TrackEnd += OutputDevice_PlaybackStopped;
                Dispatcher.UIThread.InvokeAsync(() => PB.Value = 0);
                Dispatcher.UIThread.InvokeAsync(() => LT.Text = TimeSpan.Zero.ToString());
                token = new();
                th = new Thread(() => SndThrd(token.Token));
                if (Player?.Length() is TimeSpan totalusable)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PB.Maximum = totalusable.TotalMilliseconds;
                        RT.Text = totalusable.ToString();
                    });
                }
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
                                Logic.log.Error(ex, "Error loading image into main window");
                            }
                        }
                    }
                    else
                    {
                        Dispatcher.UIThread.InvokeAsync(() => Image.Source = null);
                    }
                }
            }
            if (resetsal)
            {
                StopAutoLoading = false;
            }
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

        public void RemoveTrack()
        {
            Dispatcher.UIThread.InvokeAsync(() => Title = "SilverAudioPlayer");
            Player?.Stop();
            Player = null;
            token?.Cancel();
        }

        public RepeatState LoopType {get => dc.LoopType; set => dc.LoopType = value;}

        private void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Logic.log.Information(@"Output device playback stopped
StopAutoLoading: {StopAutoLoading}
Current song: {CurrentSong}
Loop mode: {LoopType}", StopAutoLoading, CurrentSong, LoopType);

            if (LoopType == RepeatState.One && !StopAutoLoading)
            {
                //Loop
                StartPlaying();
            }
            else if (CurrentSong != null && !StopAutoLoading)
            {
                var a = Songs.IndexOf(CurrentSong);
                if (a != -1)
                {
                    if (a + 1 < Songs.Count)
                    {
                        HandleSongChanging(Songs[a + 1], true);
                    }
                    else if (LoopType == RepeatState.Queue)
                    {
                        HandleSongChanging(Songs[0], true);
                    }
                    else
                    {
                        Logic.log.Information("RemoveTrack in PlaybackStopped");
                        RemoveTrack();
                    }
                }
                else if (NextSong != null)
                {
                    HandleSongChanging(NextSong, true);
                    NextSong = null;
                }
            }
            StopAutoLoading = false;
        }

        private void HandleSongChanging(Song NextSong, bool resetsal = false)
        {
            Logic.log.Information("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            var curr = Songs.IndexOf(CurrentSong);
            var next = Songs.IndexOf(NextSong);
            if (next == -1)
            {
                Logic.log.Information("!!!!!!! NEXT is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong}\nNextSong.URI is {NextSongURI}\nCurrentSong.Guid is {CurrentSon}\nCurrentSong.URI is {CurrentSongURI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
            else
            {
                CurrentSong = NextSong;
                RemoveTrack();
                StartPlaying(resetsal: resetsal);
            }
            if (curr == -1)
            {
                Logic.log.Information("!!!!!!! CURR is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong}\nNextSong.URI is {NextSongURI}\nCurrentSong.Guid is {CurrentSong}\nCurrentSong.URI is {CurrentSongURI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
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
                ProcessFiles(e!.Data.GetFileNames());
            }
            if(e.Data.Contains("UniformResourceLocatorW"))
            {
                ProcessFiles(new[] { e!.Data.GetText() });

            }
        }

        private void FillMetadataOfLoadedFiles(bool sortafterwards = false)
        {
            Parallel.ForEach(Songs, (Song ayo) =>
            {
                if (ayo is Song song && song.Metadata == null)
                {
                    var a = Logic.GetMetadataFromStream(song.Stream);
                    Logic.log.Information("Getting metadata in FMOLF about song " + song.Guid);
                    if (a != null)
                    {
                        Logic.log.Verbose("a wasn't null");
                        Task.Run(async () => song.Metadata = await a);
                    }
                }
            });

            if (sortafterwards)
            {
                Logic.log.Information("Sorting after filling metadata");
                List<Song> songs = new();
                foreach (var group in Songs.GroupBy(a => a?.Metadata?.DiscNumber ?? 0).OrderBy(a => a.Key))
                {
                    songs.AddRange(group.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
                }
                Songs = new ObservableCollection<Song>(songs);
                mainListBox.Items = Songs;
                mainListBox.InvalidateVisual();
            }
        }

        public void ProcessFiles(IEnumerable<string> files)
        {
            if (files?.Any() == true)
            {
                if (files.Count() == 1 && Directory.Exists(files.First()))
                {
                    files = Directory.GetFiles(files.First());
                }
                files = Logic.FilterFiles(files);
                foreach (var file in files)
                {
                    AddSong(new Song(file, file, Guid.NewGuid()));
                }
                mainListBox.Items = Songs;
                mainListBox.InvalidateVisual();
                FillMetadataOfLoadedFiles(true);
            }
        }

        public void ProcessStreams(IEnumerable<WrappedStream> files)
        {
            if (files?.Any() == true)
            {
                foreach (var file in files)
                {
                    AddSong(new Song(file, "unknown", Guid.NewGuid()));
                }
                mainListBox.Items = Songs;
                mainListBox.InvalidateVisual();
                FillMetadataOfLoadedFiles(true);
            }
        }

        public void ProcessStream(WrappedStream file)
        {
            if (file != null)
            {
                AddSong(new Song(file, "unknown", Guid.NewGuid()));
                mainListBox.Items = Songs;
                mainListBox.InvalidateVisual();
                FillMetadataOfLoadedFiles(true);
            }
        }

        public void ClearAll(object sender, RoutedEventArgs e)
        {
            Songs.Clear();
        }

        public async void AddFilee(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new()
            {
                Title = "Add file or files to the queue",
                AllowMultiple = true,
                Filters = new()
                {
                    new FileDialogFilter() { Name = "Audio Files", Extensions = { "wav", "wave" , "flac", "ogg", "mp3", "mid", "midi" ,"aif", "aiff", "aac" } },
                    new FileDialogFilter() { Name = "WAV Files", Extensions = { "wav", "wave" } },
                    new FileDialogFilter() { Name = "FLAC Files", Extensions = { "flac" } },
                    new FileDialogFilter() { Name = "OGG Files", Extensions = { "ogg" } },
                    new FileDialogFilter() { Name = "MP3 Files", Extensions = { "mp3" } },
                    new FileDialogFilter() { Name = "MIDI Files", Extensions = { "mid", "midi" } },
                    new FileDialogFilter() { Name = "AIFF Files", Extensions = { "aif", "aiff" } },
                    new FileDialogFilter() { Name = "AAC Files", Extensions = { "aac" } },
                    new FileDialogFilter() { Name = "Everything else", Extensions = { "*" } }
                }
            };
            var a = await fd.ShowAsync(this);
            if (a != null)
            {
                ProcessFiles(a);
            }
        }

        public void RemoveSelected(object sender, RoutedEventArgs e)
        {
            Logic.log.Information("RemoveSelected called");
            foreach (Song selected in mainListBox.SelectedItems)
            {
                if (selected == NextSong)
                {
                    Logic.log.Information("Selected is nextsong");
                    var a = Songs.IndexOf(selected);
                    if (Songs.Count > a + 1)
                    {
                        Logic.log.Information("NextSong is set the next one");
                        NextSong = Songs[a + 1];
                    }
                    else
                    {
                        Logic.log.Information("NextSong is set to null");
                        NextSong = null;
                    }
                }
                Songs.Remove(selected);
            }
        }

        private void AddSong(Song s)
        {
            Songs.Add(s);
        }
    }
}
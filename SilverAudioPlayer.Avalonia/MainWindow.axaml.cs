using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
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
    public partial class MainWindow : Window
    {
        private ObservableCollection<Song> Songs;
        public Logic Logic { get; set; } = new();
        public IPlay? Player { get; private set; }
        private Song? CurrentSong = null;
        private string? CurrentURI => CurrentSong?.URI;
        private bool StopAutoLoading = false;
        private Song? NextSong = null;
        private Thread? th;
        private CancellationTokenSource? token = new();
        private byte stateofdoingstuff = 0;
        private List<IMusicStatusInterface> musicStatusInterfaces = new();

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
            e.StartIPC();
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
            return RepeatState.None;
        }

        private byte MusicStatusInterface_GetVolume()
        {
            return 100;
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
                msI.TrackChangedNotification(currentSong);
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
            Player?.Play();
            SendIfStateIsNotNull();
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
                StartPlaying(true);
            }
        }

        private void MusicStatusInterface_PlayPause(object? sender, object e)
        {
            PlayPause(false);
        }

        private ulong MusicStatusInterface_GetDuration()
        {
            return (ulong?)(CurrentSong?.Metadata?.Duration / 1000) ?? 69;
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
            if (a != -1)
            {
                if (a + 1 < Songs.Count)
                {
                    HandleSongChanging(Songs[a + 1], true);
                }
            }
        }

        private void Previous()
        {
            var a = Songs.IndexOf(CurrentSong);
            if (a != -1)
            {
                if (a - 1 < 0)
                {
                    HandleSongChanging(Songs[a - 1], true);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            Songs = new();
            TreeView.Items = Songs;
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            Closing += (s, e) =>
            {
                if (Player != null)
                {
                    Player.Stop();
                }
            };
            PlayButton.Click += PlayButton_Click;
            PauseButton.Click += PauseButton_Click;
            StopButton.Click += StopButton_Click;
            PB.PointerReleased += PB_PointerReleased;
            TreeView.DoubleTapped += TreeView_DoubleTapped;
            Closing += MainWindow_Closing;
            Opened += MainWindow_Opened;

            //SetupDnd("Main", (s) => s.Set(DataFormats.FileNames, GetFromIList(TreeView.SelectedItems)), DragDropEffects.Copy | DragDropEffects.Link);
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            if (Logic.MusicStatusInterfaces?.Any() == true)
            {
                foreach (var dangthing in Logic.MusicStatusInterfaces.Select(x => x.Value))
                {
                    var a = dangthing;
                    GC.KeepAlive(a);
                    AddMSI(a);
                }
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            while (musicStatusInterfaces.Count != 0)
            {
                RemoveMSI(musicStatusInterfaces[0]);
            }
            if (Player != null)
            {
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
            }
            Player?.Stop();
        }

        private void TreeView_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (TreeView.SelectedItem is Song song)
            {
                HandleSongChanging(song, CurrentSong == null);
            }
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            Player?.Stop();
            Player = null;
        }

        public void Metadata_Click(object? sender, PointerPressedEventArgs e)
        {
            MetadataView a = new(CurrentSong);
            a.Show();
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

        private void PauseButton_Click(object? sender, RoutedEventArgs e)
        {
            Pause();
        }

        private void PlayButton_Click(object? sender, RoutedEventArgs e)
        {
            if (Player != null)
            {
                Play();
            }
            else
            {
                StartPlaying();
            }
        }

        private void Play()
        {
            Player?.Play();
            SendIfStateIsNotNull();
        }

        private void Pause()
        {
            Player?.Pause();
            SendIfStateIsNotNull();
        }

        public void StartPlaying(bool play = true, bool resetsal = false)
        {
            if (CurrentURI == null && CurrentSong == null)
            {
                if (Songs.Count > 0)
                {
                    CurrentSong = Songs[0];
                }
                else
                {
                    Logic.log.Information("Avoiding fatal crash");
                    return;
                }
            }
            Player = Logic.GetPlayerFromStream(CurrentSong.Stream);
            if (CurrentSong != null && CurrentSong?.Metadata == null && true)
            {
                var a = Logic.GetMetadataFromStream(CurrentSong.Stream);
                if (a != null)
                {
                    Logic.log.Information("Getting metadata in SP");
                    CurrentSong.Metadata = a.GetAwaiter().GetResult();
                }
            }
            if (Player == null)
            {
                //MessageBox.Show("I do not know how to play " + CurrentURI);
                MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("Error", "I do not know how to play " + CurrentURI).ShowDialog(this);
                return;
            }
            TrackChangedNotification(CurrentSong);
            if (play)
            {
                Player.Play();
                SendIfStateIsNotNull();

                Player.TrackEnd += OutputDevice_PlaybackStopped;
                /*ChannelsLabel.Text = $"Channels: {Player.ChannelCount()}";
                BPSLabel.Text = $"Bits per sample: {Player.GetBitsPerSample()}";
                SampleRateLabel.Text = $"Sample rate: {Player.GetSampleRate()}Hz";
                */
                // Player?.SetVolume((byte)volumeBar.Value);
                PB.Value = 0;
                LT.Text = TimeSpan.Zero.ToString();

                token = new();
                th = new Thread(() => SndThrd(token.Token));

                var total = Player?.Length();
                if (total != null)
                {
                    PB.Maximum = ((TimeSpan)total).TotalMilliseconds;
                    RT.Text = ((TimeSpan)total).ToString();
                }
                th.Start();
                if (CurrentSong?.Metadata != null)
                {
                    if (CurrentSong.Metadata.Title != null)
                    {
                        Title = CurrentSong.GetTitleOrFileName() + " - SilverAudioPlayer";
                    }
                    /*if (CurrentSong.Metadata.Artist != null)
                    {
                        ArtistLabel.Text = CurrentSong.Metadata.Artist;
                    }*/
                    /*if (CurrentSong.Metadata.Album != null)
                    {
                        AlbumLabel.Text = CurrentSong.Metadata.Album;
                    }*/
                    if (CurrentSong?.Metadata?.Pictures?.Any() == true)
                    {
                        var buffer = CurrentSong.Metadata.Pictures[0].Data;
                        if (buffer != null)
                        {
                            var memstream = new MemoryStream(buffer);
                            Image.Source = new Bitmap(memstream);
                        }
                    }
                    else
                    {
                        Image.Source = null;
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
            while (PB.Value < PB.Maximum)
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
                            this.FindControl<ProgressBar>("PB").Value = (Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2)).TotalMilliseconds;
                            this.FindControl<TextBlock>("LT").Text = (Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2)).ToString();
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
                    Thread.Sleep(200);
                }
                else
                {
                    return;
                }
            }
        }

        public void RemoveTrack()
        {
            Title = "SilverAudioPlayer";
            Player?.Stop();
        }

        private void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Logic.log.Information("Output device playback stopped\nLoop single checked: {ConfigLoopSong}\nStopAutoLoading: {StopAutoLoading}\nCurrent song: {CurrentSong}", false, StopAutoLoading, CurrentSong);
            if (/*Config.LoopSong*/ false && !StopAutoLoading)
            {
                RemoveTrack();
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
                }
                else if (NextSong != null)
                {
                    HandleSongChanging(NextSong, true);
                    NextSong = null;
                }
            }
            else if (StopAutoLoading)
            {
                StopAutoLoading = false;
            }
            /*else if (preventstoppedstatus)
            {
                TrackLmao(TrackEvent.EndOfTrack);
            }
            else
            {
                preventstoppedstatus = false;
            }*/
            StopAutoLoading = false;
            // SendIfStateIsNotNull();
        }

        private void HandleSongChanging(Song NextSong, bool resetsal = false)
        {
            Logic.log.Information("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            var curr = Songs.IndexOf(CurrentSong);
            var next = Songs.IndexOf(NextSong);
            if (next == -1)
            {
                Logic.log.Information("!!!!!!! NEXT is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong.Guid}\nNextSong.URI is {NextSong.URI}\nCurrentSong.Guid is {CurrentSong.Guid}\nCurrentSong.URI is {CurrentSong.URI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
            else
            {
                CurrentSong = NextSong;
                RemoveTrack();
                if (curr != -1)
                {
                    TreeView.SelectedItem = next;
                    //curr.ForeColor = next.ForeColor;
                }
                //next.ForeColor = Color.LightGreen;
                StartPlaying(resetsal: resetsal);
            }
            if (curr == -1)
            {
                Logic.log.Information("!!!!!!! CURR is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong.Guid}\nNextSong.URI is {NextSong.URI}\nCurrentSong.Guid is {CurrentSong.Guid}\nCurrentSong.URI is {CurrentSong.URI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
        }

        private string[] GetFromIList(System.Collections.IList list)
        {
            List<string> a = new();
            foreach (var item in list)
            {
                a.Add(((Song)item).URI);
            }
            return a.ToArray();
        }

        private void DragOver(object sender, DragEventArgs e)
        {
            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects &= (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects &= (DragDropEffects.Copy);
            }
            Debug.WriteLine($"{string.Join(' ', e.Data.GetDataFormats())}");
            if (!e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.None;
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
                ProcessFiles(e.Data.GetFileNames());
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
                        Task.Run(async () =>
                        {
                            song.Metadata = await a;
                        });
                    }
                }
            });

            if (sortafterwards)
            {
                Logic.log.Information("Sorting after filling metadata");
                List<Song> songs = new();
                var groups = Songs.GroupBy(a => a?.Metadata?.DiscNumber ?? 0).OrderBy(a => a.Key);
                foreach (var group in groups)
                {
                    songs.AddRange(group.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
                }
                Songs = new ObservableCollection<Song>(songs);
                TreeView.Items = Songs;
                TreeView.InvalidateVisual();
            }
        }

        private void ProcessFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                AddSong(new Song(file, file, Guid.NewGuid()));
            }
            TreeView.Items = Songs;
            TreeView.InvalidateVisual();
            Debug.WriteLine(Songs.Count);
            FillMetadataOfLoadedFiles(true);
        }

        public void ClearAll(object sender, RoutedEventArgs e)
        {
            Songs.Clear();
        }

        public async void AddFilee(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new();
            fd.Title = "Add file or files to the queue";
            fd.AllowMultiple = true;
            fd.Filters.Add(new FileDialogFilter() { Name = "WAV Files", Extensions = { "wav", "wave" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "FLAC Files", Extensions = { "flac" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "OGG Files", Extensions = { "ogg" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "MP3 Files", Extensions = { "mp3" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "MIDI Files", Extensions = { "mid", "midi" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "AIFF Files", Extensions = { "aif", "aiff" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "AAC Files", Extensions = { "aac" } });
            fd.Filters.Add(new FileDialogFilter() { Name = "Everything else", Extensions = { "*" } });
            var a = await fd.ShowAsync(this);
            if (a != null)
            {
                ProcessFiles(a);
            }
        }

        public void RemoveSelected(object sender, RoutedEventArgs e)
        {
            foreach (Song selected in TreeView.SelectedItems)
            {
                if (selected == NextSong)
                {
                    var a = Songs.IndexOf(selected);
                    if (Songs.Count > a + 1)
                    {
                        NextSong = Songs[a + 1];
                    }
                    else
                    {
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
using Microsoft.Win32;
using SilverAudioPlayer.CAD;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Winforms;
using SilverConfig;
using SilverFormsUtils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;

namespace SilverAudioPlayer
{
    public partial class Form1 : Form
    {
        private IConfigReader<Preferences> ConfigReader;
        private const string ConfigFileName = "settings\\silveraudioplayer.winforms.preferences.xml";
        private Preferences Config;
        private bool WatchForConfigChanges = true;
        private ConfigFileWatcher cfw;
        private string ConfigLoc;
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
                Invoke(() => { volumeBar.Value = e; });
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
            return Config.LoopSong ? RepeatState.One : RepeatState.None;
        }

        private byte MusicStatusInterface_GetVolume()
        {
            return Invoke(() => { return (byte)volumeBar.Value; });
        }

        private PlaybackState MusicStatusInterface_GetState()
        {
            return Player?.GetPlaybackState() ?? PlaybackState.Stopped;
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

            e.StopIPC();
            e.Dispose();
            musicStatusInterfaces.Remove(e);
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
            Invoke(() =>
            {
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                int index = aa.First(x => (Song)x.Tag == CurrentSong).Index;
                if (index - 1 >= 0)
                {
                    StopAutoLoading = true;
                    HandleSongChanging((Song)aa[index - 1].Tag);
                }
            });
        }

        private void MusicStatusInterface_Next(object? sender, EventArgs e)
        {
            Invoke(() =>
            {
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                int index = aa.First(x => (Song)x.Tag == CurrentSong).Index;
                if (index + 1 < aa.Length)
                {
                    StopAutoLoading = true;
                    HandleSongChanging((Song)aa[index + 1].Tag);
                }
            });
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

        private void MusicStatusInterface_Play(object? sender, object e)
        {
            Player?.Play();
            SendIfStateIsNotNull();
        }

        public Form1()
        {
            InitializeComponent();
            ConfigLoc = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            ConfigReader = new CommentXmlConfigReader<Preferences>();
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, ConfigFileName)))
            {
                Config = ConfigReader.Read(ConfigLoc);
            }
            else
            {
                Config = new Preferences();
                ConfigReader.Write(Config, ConfigLoc);
            }

            ProgressBar.ProgressBar.MouseClick += ProgressBar_MouseClick;
            OnConfigChange(true);
            AutosaveConfig.Elapsed += AutosaveConfig_Elapsed;
            AutosaveConfig.Start();
            if (WatchForConfigChanges)
            {
                cfw = new(ConfigLoc);
                cfw.OnChangedE += Cfw_OnChangedE;
            }
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
        }

        public Form1(params string[] files) : this()
        {
            ProcessFiles(true, files);
        }

        private void AutosaveConfig_Elapsed(object? sender, ElapsedEventArgs e)
        {
            AutoSaveConfig();
        }

        private System.Timers.Timer AutosaveConfig = new();

        private void Cfw_OnChangedE(object? sender, string e)
        {
            if (savednow)
            {
                savednow = false;
            }
            else
            {
                Config = ConfigReader.Read(ConfigLoc);
                OnConfigChange();
            }
        }

        private bool HotKeyRegistered = false;
        private bool savednow = false;

        private void AutoSaveConfig()
        {
            savednow = true;
            Logic.log.Information("Saving config");
            volumeBar.Invoke(() => { Config.Volume = (byte)volumeBar.Value; });
            Config.ProgressBarRainbow = ProgressBar.ProgressBar.Rainbow;
            Config.ProgressBarRainbowShift = ProgressBar.ProgressBar.ShiftRainbow;
            Config.ProgressBarRainbowCaching = (byte)ProgressBar.ProgressBar.CacheRainbowDecimals;
            Config.ProgressBarColour = ProgressBar.ProgressBar.Color.ToArgb();
            Config.MillisecondIntervalOfAutoSave = (ulong)AutosaveConfig.Interval;
            ConfigReader.Write(Config, ConfigLoc);
            Logic.log.Information("Config saved");
        }

        private void OnConfigChange(bool fromstartup = false)
        {
            ProgressBar.ProgressBar.ShiftRainbow = Config.ProgressBarRainbowShift;
            ProgressBar.ProgressBar.CacheRainbowDecimals = Config.ProgressBarRainbowCaching;
            ProgressBar.ProgressBar.Rainbow = Config.ProgressBarRainbow;
            ProgressBar.ProgressBar.Color = Color.FromArgb(Config.ProgressBarColour);
            if (fromstartup)
            {
                volumeBar.Value = Config.Volume > 100 ? 100 : Config.Volume;
                if (Config.Volume > 100)
                {
                    Config.Volume = 100;
                }
                Player?.SetVolume(Config.Volume);
            }
            else
            {
                volumeBar.Invoke(() => { volumeBar.Value = Config.Volume > 100 ? 100 : Config.Volume; });
                if (Config.Volume > 100)
                {
                    Config.Volume = 100;
                }
                Player?.SetVolume(Config.Volume);
            }
            if (Config!.ProcessMessages && !HotKeyRegistered && Config.HandleMediaControls)
            {
                if (fromstartup)
                {
                    RegisterHotKey(Handle, 1, 0, (int)Keys.Play);
                    RegisterHotKey(Handle, 2, 0, (int)Keys.Pause);
                    RegisterHotKey(Handle, 3, 0, (int)Keys.MediaPlayPause);
                }
                else
                {
                    Invoke(() =>
                    {
                        RegisterHotKey(Handle, 1, 0, (int)Keys.Play);
                        RegisterHotKey(Handle, 2, 0, (int)Keys.Pause);
                        RegisterHotKey(Handle, 3, 0, (int)Keys.MediaPlayPause);
                    });
                }

                HotKeyRegistered = true;
            }
            else if (((!Config!.ProcessMessages) || !Config.HandleMediaControls) && HotKeyRegistered)
            {
                if (fromstartup)
                {
                    UnregisterHotKey(Handle, 1);
                    UnregisterHotKey(Handle, 2);
                    UnregisterHotKey(Handle, 3);
                }
                else
                {
                    Invoke(() =>
                    {
                        UnregisterHotKey(Handle, 1);
                        UnregisterHotKey(Handle, 2);
                        UnregisterHotKey(Handle, 3);
                    });
                }

                HotKeyRegistered = false;
            }
            AutosaveConfig.Interval = Config.MillisecondIntervalOfAutoSave;
        }

        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_AUDIOPLAYERZ");

        [DllImport("user32")]
        private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string message);

        private void ShowMe()
        {
            if (Config.AutoMagicallyLoadFromArgstxt || MessageBox.Show("Would you like to load the files you tried to load in a different instance?", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "args.txt");

                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    ProcessFiles(false, lines);
                }
            }
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            // make our form jump to the top of everything
            TopMost = true;
            Activate();
            // set it back to whatever it was
            TopMost = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (Config!.ProcessMessages)
            {
                if (m.Msg == WM_SHOWME)
                {
                    ShowMe();
                }
                if (Config.HandleMediaControls)
                {
                    switch (m.Msg)
                    {
                        case WM_APPCOMMAND:
                            int cmd = (int)m.LParam >> 16 & 0xFF;
                            switch (cmd)
                            {
                                case APPCOMMAND_MEDIA_PLAY_PAUSE:
                                    PlayPause(true);
                                    break;

                                default:
                                    break;
                            }
                            m.Result = (IntPtr)1;
                            break;

                        case 0x0312:
                            switch (m.WParam.ToInt32())
                            {
                                case 1:
                                    Play();
                                    break;

                                case 2:
                                    Pause();
                                    break;

                                case 3:
                                    PlayPause(true);
                                    break;
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
            base.WndProc(ref m);
        }

        private CancellationTokenSource? token = new();
        private byte stateofdoingstuff = 0;
        private string? CurrentURI => CurrentSong?.URI;
        private bool StopAutoLoading = false;
        private Song? CurrentSong = null;
        public IPlay? Player { get; private set; }
        private Thread? th;

        public Logic Logic { get; set; } = new();

        public void StartPlaying(bool play = true, bool resetsal = false)
        {
            if (CurrentURI == null && CurrentSong == null)
            {
                if (treeView1.Nodes[0].Nodes.Count > 0)
                {
                    CurrentSong = (Song)treeView1.Nodes[0].Nodes[0].Tag;
                }
                else
                {
                    Logic.log.Information("Avoiding fatal crash");
                    return;
                }
            }
            Player = Logic.GetPlayerFromStream(CurrentSong.Stream);
            if (CurrentSong != null && CurrentSong?.Metadata == null && Config.CheckForMetadataInSP)
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
                MessageBox.Show("I do not know how to play " + CurrentURI);
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
                Player?.SetVolume((byte)volumeBar.Value);
                ProgressBar.Pos = TimeSpan.FromMilliseconds(0);
                token = new();
                th = new Thread(() => SndThrd(token.Token));

                var total = Player?.Length();
                if (total != null)
                {
                    ProgressBar.Max = (TimeSpan)total;
                }
                th.Start();
                if (CurrentSong?.Metadata != null)
                {
                    if (CurrentSong.Metadata.Title != null)
                    {
                        Text = CurrentSong.Metadata.Title + " - SilverAudioPlayer";
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
                            using var memstream = new MemoryStream(buffer);
                            pictureBox1.Image = Image.FromStream(memstream);
                        }
                    }
                    else
                    {
                        pictureBox1.Image = null;
                    }
                }
            }
            if (resetsal)
            {
                StopAutoLoading = false;
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

        private void SndThrd(CancellationToken e)
        {
            MethodInvoker m = new(() => ProgressBar.Pos = (Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2)));
            while (ProgressBar.Pos < ProgressBar.Max)
            {
                if (Player?.GetPlaybackState() == PlaybackState.Playing)
                {
                    if (e.IsCancellationRequested || !Visible)
                    {
                        return;
                    }
                    try
                    {
                        ProgressBar.Invoke(m);
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
            Text = "SilverAudioPlayer";
            Player?.Stop();
        }

        private void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Logic.log.Information("Output device playback stopped\nLoop single checked: {ConfigLoopSong}\nStopAutoLoading: {StopAutoLoading}\nCurrent song: {CurrentSong}", Config.LoopSong, StopAutoLoading, CurrentSong);
            if (Config.LoopSong && !StopAutoLoading)
            {
                RemoveTrack();
                StartPlaying();
            }
            else if (CurrentSong != null && !StopAutoLoading)
            {
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                var a = aa.FirstOrDefault(x => (Song)x.Tag == CurrentSong);

                if (a != null)
                {
                    int index = a.Index;
                    if (index + 1 < aa.Length)
                    {
                        HandleSongChanging((Song)aa[index + 1].Tag, true);
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
            SendIfStateIsNotNull();
        }

        private void HandleSongChanging(Song NextSong, bool resetsal = false)
        {
            Logic.log.Information("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            var curr = FindById(CurrentSong?.Guid);
            var next = FindById(NextSong.Guid);
            if (next == null)
            {
                Logic.log.Information("!!!!!!! NEXT is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong.Guid}\nNextSong.URI is {NextSong.URI}\nCurrentSong.Guid is {CurrentSong.Guid}\nCurrentSong.URI is {CurrentSong.URI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
            else
            {
                CurrentSong = (Song)next.Tag;
                RemoveTrack();
                if (curr != null)
                {
                    curr.ForeColor = next.ForeColor;
                }
                next.ForeColor = Color.LightGreen;
                StartPlaying(resetsal: resetsal);
            }
            if (curr == null)
            {
                Logic.log.Information("!!!!!!! CURR is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong.Guid}\nNextSong.URI is {NextSong.URI}\nCurrentSong.Guid is {CurrentSong.Guid}\nCurrentSong.URI is {CurrentSong.URI}", NextSong.Guid, NextSong.URI, CurrentSong.Guid, CurrentSong.URI);
            }
        }

        private void DragOverM(object sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private IEnumerable<string> FilterFiles(IEnumerable<string> files)
        {
            return Logic.FilterFiles(files);
        }

        private void DragDropOp(object sender, DragEventArgs e)
        {
            if (stateofdoingstuff != 0)
            {
                return;
            }
            string[]? files = null;
            if (e.Data?.GetDataPresent("UniformResourceLocatorW") == true)
            {
                files = new string[] { System.Text.Encoding.Unicode.GetString(((MemoryStream)e.Data.GetData("UniformResourceLocatorW")).ToArray()) };
            }
            else if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                files = (string[])e.Data.GetData(DataFormats.FileDrop);
            }
            ProcessFiles(false, files);
        }

        public void ProcessFiles(bool fromProgram = false, params string[] files)
        {
            if (files.Length == 1 && Directory.Exists(files[0]))
            {
                files = FilterFiles(Directory.GetFiles(files[0])).ToArray();
            }
            if ((files.Length > 0))
            {
                files = FilterFiles(files).ToArray();
                var a = treeView1.Nodes[0].Nodes.Count;
                treeView1.Nodes[0].Nodes.AddRange(files.Select(x => new TreeNode(x) { Tag = new Song(x, x, Guid.NewGuid()) }).ToArray());
                treeView1.ExpandAll();
                if (Config.FillMetadataOfLoadedFilesOnLoad)
                {
                    FillMetadataOfLoadedFiles(true);
                }
                if (treeView1.Nodes[0].Nodes.Count != 0 && (Config!.PlayAfterSelect && ((a == 0 && (Player == null || Player?.GetPlaybackState() == PlaybackState.Stopped)) || fromProgram || MessageBox.Show("Would you like to skip to the newly added part?", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes)))
                {
                    if (fromProgram)
                    {
                        th = new Thread(() =>
                        {
                            Thread.Sleep(500);
                            HandleSongChanging((Song)treeView1.Nodes[0].Nodes[0].Tag, true);
                        });
                        th.Start();
                    }
                    else
                    {
                        HandleSongChanging((Song)treeView1.Nodes[0].Nodes[a].Tag, (a == 0 && (Player == null || Player?.GetPlaybackState() == PlaybackState.Stopped)));
                    }
                }
            }
        }

        private void FillMetadataOfLoadedFiles(bool sortafterwards = false)
        {
            TreeNode[]? pf = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(pf, 0);
            Parallel.ForEach(pf, (TreeNode ayo) =>
            {
                if (ayo.Tag is Song song && song.Metadata == null)
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
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                treeView1.Nodes[0].Nodes.Clear();
                foreach (var song in aa.OrderBy(x => ((Song?)x?.Tag)?.Metadata?.TrackNumber ?? 0))
                {
                    Debug.WriteLine(((Song?)song?.Tag)?.Metadata?.TrackNumber);

                    Logic.log.Information("Adding song " + ((Song)song.Tag).Guid + " to treeview");
                    treeView1.Nodes[0].Nodes.Add(song);
                }
            }
        }

        private TreeNode? SourceNode;
        private string[]? filetodrop = null;

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UniformResourceLocatorW"))
            {
                e.Effect = DragDropEffects.Link;
                return;
            }
            Debug.WriteLine(string.Join(" ", e.Data.GetFormats()));
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            var data = (TreeNode?)e.Data?.GetData("System.Windows.Forms.TreeNode");
            if (SourceNode != null)
            {
                e.Effect = DragDropEffects.Move;
                return;
            }
            else if (data != null)
            {
                SourceNode = data;
                e.Effect = DragDropEffects.Move;
                return;
            }
        }

        private void TreeViewDragDrop(object sender, DragEventArgs e)
        {
            stateofdoingstuff = 1;
            if ((e.Data?.GetDataPresent(DataFormats.FileDrop) == true || e.Data?.GetDataPresent("UniformResourceLocatorW") == true) && SourceNode == null)
            {
                stateofdoingstuff = 0;
                DragDropOp(sender, e);
                return;
            }
            stateofdoingstuff = 1;
            Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
            TreeNode DestinationNode = ((TreeView)sender).GetNodeAt(pt);
            if (DestinationNode == null)
            {
                var tree = (TreeView)sender;
                var lstnod = tree.Nodes[0].Nodes[^1];
                if (lstnod != null)
                {
                    Logic.log.Debug(lstnod.Bounds.X + " " + lstnod.Bounds.Y);
                    if (pt.Y > lstnod.Bounds.Y && pt.X > lstnod.Bounds.X && pt.X < (lstnod.Bounds.X + lstnod.Bounds.Right))
                    {
                        DestinationNode = lstnod;
                    }
                }
            }
            if (DestinationNode == null)
            {
                return;
            }
            if (SourceNode != null)
            {
                try
                {
                    //int sourceIndex = SourceNode.Index;
                    int destIndex = DestinationNode.Index;
                    if (SourceNode.Level == 1 && DestinationNode.Level == 1)
                    {
                        TreeNode parentNode = SourceNode.Parent;
                        SourceNode.Remove();
                        DestinationNode.Remove();
                        parentNode.Nodes.Insert(destIndex, DestinationNode);
                        parentNode.Nodes.Insert(destIndex, SourceNode);
                    }
                }
                catch (Exception ex)
                {
                    Logic.log.Error(ex.Message);
                }
                SourceNode = null;
            }
            stateofdoingstuff = 0;
        }

        private void treeView1_MouseLeave(object sender, EventArgs e)
        {
            if (filetodrop != null)
            {
                treeView1.DoDragDrop(new DataObject(DataFormats.FileDrop, filetodrop), DragDropEffects.Copy |
 DragDropEffects.Move);
                filetodrop = null;
            }
        }

        private void treeView1_MouseUp(object sender, MouseEventArgs e)
        {
            filetodrop = null;
        }

        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (treeView1.SelectedNode != null && e.Button == MouseButtons.Left)
            {
                if (treeView1.SelectedNode == treeView1.TopNode)
                {
                    List<string> sb = new();
                    foreach (TreeNode a in treeView1.Nodes[0].Nodes)
                    {
                        sb.Add(a.Text);
                    }
                    if (sb.Count > 0)
                    {
                        filetodrop = sb.ToArray();
                    }
                    else
                    {
                        filetodrop = null;
                    }
                }
                else
                {
                    filetodrop = new string[] { treeView1.SelectedNode.Text };
                }
            }
        }

        private void volumeBar_Scroll(object sender, EventArgs e)
        {
            Player?.SetVolume((byte)volumeBar.Value);
        }

        private void PlayButton_Click(object sender, EventArgs e)
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

        private void PauseButton_Click(object sender, EventArgs e)
        {
            Pause();
        }

        private TreeNode SelectedItem;

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                switch (e.Node.Tag)
                {
                    case null:
                        contextMenuStrip2.Show(treeView1, new Point(e.X, e.Y));
                        break;

                    default:
                        contextMenuStrip1.Show(treeView1, new Point(e.X, e.Y));
                        SelectedItem = e.Node;
                        break;
                }
            }
            treeView1.SelectedNode = e.Node;
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Node.Tag != null)
            {
                HandleSongChanging((Song)e.Node.Tag);
            }
        }

        private void playNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedItem != null)
            {
                HandleSongChanging((Song)SelectedItem.Tag);
            }
        }

        private void upToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveSel(-1);
        }

        private void downToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveSel(+1);
        }

        private TreeNode? FindById(Guid? songID)
        {
            if (songID == null)
            {
                return null;
            }
            foreach (TreeNode TreeNode in treeView1.Nodes[0].Nodes)
            {
                if (((Song)TreeNode.Tag).Guid == songID)
                {
                    return TreeNode;
                }
            }
            return null;
        }

        private void MoveSel(short where)
        {
            MoveTrack(where, SelectedItem);
        }

        private void MoveTrack(short howmuch, TreeNode what)
        {
            treeView1.Nodes[0].Nodes.RemoveAt(what.Index);
            treeView1.Nodes[0].Nodes.Insert(what.Index + howmuch, what);
        }

        private void playNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            if (CurrentSong == (Song)SelectedItem.Tag)
            {
                MessageBox.Show("You thought");
                return;
            }
            int indexb = aa.First(x => x == SelectedItem).Index;
            int indexa = aa.First(x => (Song)x.Tag == CurrentSong).Index;
            treeView1.Nodes[0].Nodes.RemoveAt(indexb);
            treeView1.Nodes[0].Nodes.Insert(indexa + 1, aa[indexb]);
            treeView1.SelectedNode = aa[indexb];
        }

        private Song? NextSong = null;

        private void removeFromQueueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedItem != null)
            {
                if (SelectedItem.Tag == CurrentSong)
                {
                    if (treeView1.Nodes[0].Nodes.Count > SelectedItem.Index + 1)
                    {
                        NextSong = treeView1.Nodes[0].Nodes[SelectedItem.Index + 1].Tag as Song;
                    }
                    else
                    {
                        NextSong = null;
                    }
                }
                treeView1.Nodes[0].Nodes.Remove(SelectedItem);
            }
        }

        private void clearQueueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.Nodes[0].Nodes.Clear();
            CurrentSong = null;
        }

        private void importFromClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string? text = Clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                string[]? lines = text.Split(Environment.NewLine);
                if (lines != null)
                {
                    ProcessFiles(false, lines);
                }
            }
        }

        private void exportToClipboardnewLineSeperatedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder? sb = new();
            foreach (TreeNode a in treeView1.Nodes[0].Nodes)
            {
                sb.AppendLine(a.Text);
            }
            Clipboard.SetText(sb.ToString());
        }

        private void ProgressBar_MouseClick(object sender, MouseEventArgs e)
        {
            Point seekpoint = ProgressBar.ProgressBar.PointToClient(Cursor.Position);
            var a = ProgressBar.Min + ((ProgressBar.Max - ProgressBar.Min) * ((ulong)seekpoint.X) / ((ulong)ProgressBar.ProgressBar.Width));
            ProgressBar.Pos = a;
            Player?.SetPosition(a);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cfw.Dispose();
            AutoSaveConfig();
            AutosaveConfig.Stop();
            AutosaveConfig.Dispose();
            while (musicStatusInterfaces.Count != 0)
            {
                RemoveMSI(musicStatusInterfaces[0]);
            }
            if (Player != null)
            {
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
            }
            Player?.Stop();
            token.Cancel();
            cfw.Dispose();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            AutoSaveConfig();
            Process.Start("notepad.exe", ConfigLoc);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Player?.Stop();
            Player = null;
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (Logic.MusicStatusInterfaces?.Any() == true)
            {
                foreach (var dangthing in Logic.MusicStatusInterfaces.Where(x => x.Value != null).Select(x => x.Value))
                {
                    var a = dangthing;
                    GC.KeepAlive(a);
                    AddMSI(a);
                    if (a is CADMusicStatusInterface ee)
                    {
                        ee.Show();
                    }
                }
            }
        }

        private MetadataForm mf;

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (CurrentSong != null)
            {
                if (mf != null)
                {
                    mf.Close();
                    mf.Dispose();
                }
                mf = new(ref CurrentSong);
                mf.Show();
            }
        }
    }
}
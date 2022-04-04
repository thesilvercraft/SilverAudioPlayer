using Microsoft.Win32;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
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
        private const string ConfigFileName = "preferences.xml";
        private Preferences Config;
        private bool WatchForConfigChanges = true;
        private ConfigFileWatcher cfw;
        private string ConfigLoc;

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
            Debug.WriteLine("Saving config");
            volumeBar.Invoke(() => { Config.Volume = (byte)volumeBar.Value; });
            Config.ProgressBarRainbow = ProgressBar.ProgressBar.Rainbow;
            Config.ProgressBarRainbowShift = ProgressBar.ProgressBar.ShiftRainbow;
            Config.ProgressBarRainbowCaching = (byte)ProgressBar.ProgressBar.CacheRainbowDecimals;
            Config.ProcessMessages = HotKeyRegistered;
            Config.ProgressBarColour = ProgressBar.ProgressBar.Color.ToArgb();
            Config.MillisecondIntervalOfAutoSave = (ulong)AutosaveConfig.Interval;
            ConfigReader.Write(Config, ConfigLoc);
            Debug.WriteLine("Config saved");
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
            if (Config!.ProcessMessages && !HotKeyRegistered)
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
            else if ((!Config!.ProcessMessages) && HotKeyRegistered)
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
                void Toggle()
                {
                    if (Player?.GetPlaybackState() == PlaybackState.Playing)
                    {
                        Player?.Pause();
                    }
                    else if (Player?.GetPlaybackState() == PlaybackState.Paused)
                    {
                        Player?.Play();
                    }
                    else
                    {
                        StartPlaying(true);
                    }
                }
                switch (m.Msg)
                {
                    case WM_APPCOMMAND:
                        int cmd = (int)m.LParam >> 16 & 0xFF;
                        switch (cmd)
                        {
                            case APPCOMMAND_MEDIA_PLAY_PAUSE:
                                Toggle();
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
                                Player?.Play();
                                break;

                            case 2:
                                Player?.Pause();
                                break;

                            case 3:
                                Toggle();
                                break;
                        }
                        break;

                    default:
                        break;
                }
            }
            base.WndProc(ref m);
        }

        [DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private readonly string[] AssociatedFileTypes = new[] { ".mp3", ".aif", ".aiff", ".flac", ".wav", ".ogg" };

        public void RegisterInReg()
        {
            if (string.IsNullOrEmpty((string?)Registry.GetValue("HKEY_CLASSES_ROOT\\SilverAudioPlayer", string.Empty, string.Empty)))
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer", "", "Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer", "FriendlyTypeName", "AudioPlayerZ Audio File");
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Classes\\SilverAudioPlayer\\shell\\open\\command", "",
                    $"{Environment.ProcessPath} \"%1\"");
                foreach (string? type in AssociatedFileTypes)
                {
                    string? a = $"HKEY_CURRENT_USER\\Software\\Classes\\{type}";
                    string? val = (string?)Registry.GetValue(a, "", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        StringBuilder name = new("SAP.BAK");
                        string? val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                        while (!string.IsNullOrEmpty(val2))
                        {
                            name.Append(".BAK");
                            val2 = (string?)Registry.GetValue(a, name.ToString(), "");
                        }
                        Registry.SetValue(a, name.ToString(), val);
                    }
                    Registry.SetValue(a, "", "SilverAudioPlayer");
                }
                //this call notifies Windows that it needs to redo the file associations and icons
                _ = SHChangeNotify(0x08000000, 0x2000, IntPtr.Zero, IntPtr.Zero);
            }
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
                CurrentSong = (Song)treeView1.Nodes[0].Nodes[0].Tag;
            }
            Player = Logic.GetPlayerFromURI(CurrentURI);
            if (CurrentSong != null && CurrentSong?.Metadata == null && Config.CheckForMetadataInSP)
            {
                var a = Logic.GetMetadataFromURI(CurrentURI);
                if (a != null)
                {
                    Debug.WriteLine("Getting metadata in SP");
                    CurrentSong.Metadata = a.GetAwaiter().GetResult();
                }
            }
            if (Player == null)
            {
                MessageBox.Show("I do not know how to play " + CurrentURI);
                return;
            }
            if (play)
            {
                Player.Play();
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
                        Debug.WriteLine(ex.Message);
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
            Debug.WriteLine("Output device playback stopped");
            Debug.WriteLine("Loop single checked: " + Config.LoopSong);
            Debug.WriteLine("StopAutoLoading: " + StopAutoLoading);
            Debug.WriteLine("Current song: " + CurrentSong);
            if (Config.LoopSong && !StopAutoLoading)
            {
                RemoveTrack();
                StartPlaying();
            }
            else if (CurrentSong != null && !StopAutoLoading)
            {
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                int index = aa.First(x => (Song)x.Tag == CurrentSong).Index;
                if (index + 1 < aa.Length)
                {
                    HandleSongChanging((Song)aa[index + 1].Tag, true);
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
        }

        private void HandleSongChanging(Song NextSong, bool resetsal = false)
        {
            Debug.WriteLine("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            var curr = FindById(CurrentSong?.Guid);
            var next = FindById(NextSong.Guid);
            if (next == null)
            {
                Debug.WriteLine("!!!!!!! NEXT is NULL in HSC !!!!!!!!");
                Debug.WriteLine("NextSong.Guid is " + NextSong.Guid);
                Debug.WriteLine("NextSong.URI is " + NextSong.URI);
                Debug.WriteLine("CurrentSong.Guid is " + CurrentSong.Guid);
                Debug.WriteLine("CurrentSong.URI is " + CurrentSong.URI);
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
                Debug.WriteLine("!!!!!!! CURR is NULL in HSC !!!!!!!!");
                Debug.WriteLine("CurrentSong.Guid is " + CurrentSong.Guid);
                Debug.WriteLine("CurrentSong.URI is " + CurrentSong.URI);
                Debug.WriteLine("NextSong.Guid is " + NextSong.Guid);
                Debug.WriteLine("NextSong.URI is " + NextSong.URI);
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
            string[]? files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ProcessFiles(false, files);
        }

        public void ProcessFiles(bool fromProgram = false, params string[] files)
        {
            if (files.Length == 1 && Directory.Exists(files[0]))
            {
                files = FilterFiles(Directory.GetFiles(files[0])).ToArray();
            }
            if ((files.Length > 1) || files.Length == 1 && File.Exists(files[0]))
            {
                files = FilterFiles(files).ToArray();
                var a = treeView1.Nodes[0].Nodes.Count;
                treeView1.Nodes[0].Nodes.AddRange(files.Select(x => new TreeNode(x) { Tag = new Song(x, x, Guid.NewGuid()) }).ToArray());
                treeView1.ExpandAll();
                if (Config.FillMetadataOfLoadedFilesOnLoad)
                {
                    FillMetadataOfLoadedFiles(true);
                }
                if (Config!.PlayAfterSelect && ((a == 0 && (Player == null || Player?.GetPlaybackState() == PlaybackState.Stopped)) || fromProgram || MessageBox.Show("Would you like to skip to the newly added part?", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes))
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
            for (int i = 0; i < treeView1.Nodes[0].Nodes.Count; i++)
            {
                if (treeView1.Nodes[0].Nodes[i].Tag is Song song && song.Metadata == null)
                {
                    var a = Logic.GetMetadataFromURI(song.URI);
                    Debug.WriteLine("Getting metadata in FMOLF about song " + song.Guid);
                    if (a != null)
                    {
                        Debug.WriteLine("a wasn't null");
                        song.Metadata = a.GetAwaiter().GetResult();
                    }
                }
            }
            if (sortafterwards)
            {
                Debug.WriteLine("Sorting after filling metadata");
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                treeView1.Nodes[0].Nodes.Clear();
                foreach (var song in aa.OrderBy(x => ((Song)x.Tag).Metadata.TrackNumber ?? 0))
                {
                    Debug.WriteLine("Adding song " + ((Song)song.Tag).Guid + " to treeview");
                    treeView1.Nodes[0].Nodes.Add(song);
                }
            }
        }

        private TreeNode? SourceNode;
        private string[]? filetodrop = null;

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
            var data = (TreeNode?)e.Data?.GetData("System.Windows.Forms.TreeNode");
            if (SourceNode != null)
            {
                e.Effect = DragDropEffects.Move;
            }
            else if (data != null)
            {
                SourceNode = data;
                e.Effect = DragDropEffects.Move;
            }
        }

        private void TreeViewDragDrop(object sender, DragEventArgs e)
        {
            stateofdoingstuff = 1;
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true && SourceNode == null)
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
                    Debug.WriteLine(lstnod.Bounds.X + " " + lstnod.Bounds.Y);
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
                    Debug.WriteLine(ex.Message);
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
                Player.Play();
            }
            else
            {
                StartPlaying();
            }
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            Player?.Pause();
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
            Move(where, SelectedItem);
        }

        private void Move(short howmuch, TreeNode what)
        {
            treeView1.Nodes[0].Nodes.RemoveAt(what.Index);
            treeView1.Nodes[0].Nodes.Insert(what.Index + howmuch, what);
        }

        private void Move(short howmuch, Song song)
        {
            Move(howmuch, FindById(song.Guid));
        }

        private TreeNode[] GetFromPlaylist()
        {
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            return aa;
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

        private void removeFromQueueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedItem != null)
            {
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
            AutoSaveConfig();
            AutosaveConfig.Stop();
            AutosaveConfig.Dispose();
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
    }
}
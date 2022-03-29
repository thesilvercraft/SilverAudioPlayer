using SilverAudioPlayer.Shared;
using SilverFormsUtils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SilverAudioPlayer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            if (GetDarkModePreference.ShouldIUseDarkMode())
            {
                this.UseDarkModeBar(true);
                this.UseDarkModeForThingsInsideOfForm(true, true);
            }
            ProcessMessages = !File.Exists(Path.Combine(AppContext.BaseDirectory, ".nohotkeys"));
            if (ProcessMessages)
            {
                RegisterHotKey(Handle, 1, 0, (int)Keys.Play);
                RegisterHotKey(Handle, 2, 0, (int)Keys.Pause);
                RegisterHotKey(Handle, 3, 0, (int)Keys.MediaPlayPause);
            }
        }

        public Form1(params string[] files) : this()
        {
            treeView1.Nodes[0].Nodes.AddRange(files.Select(x => new TreeNode(x) { Tag = new Song(x, x, Guid.NewGuid()) }).ToArray());
        }

        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_AUDIOPLAYERZ");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);

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

        private bool ProcessMessages = true;

        protected override void WndProc(ref Message m)
        {
            if (ProcessMessages)
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

        private byte stateofdoingstuff = 0;
        private string? CurrentURI => CurrentSong?.URI;
        private bool StopAutoLoading = false;
        private Song? CurrentSong = null;
        public IPlay? Player { get; private set; }
        private Thread? th;

        private bool ShouldPlayAfterSelect() => true;

        public Logic logic { get; set; } = new();

        public void StartPlaying(bool play = true, bool resetsal = false)
        {
            if (CurrentURI == null && CurrentSong == null)
            {
                CurrentSong = (Song)treeView1.Nodes[0].Nodes[0].Tag;
            }
            Player = logic.GetPlayerFromURI(CurrentURI);
            if (Player == null)
            {
                MessageBox.Show("I do not know how to play " + CurrentURI);
                return;
            }
            /*
            if (textBox1.Text[0..4].ToLowerInvariant() == ("http"))
            {
                if (YtbR.IsMatch(textBox1.Text))
                {
                    MessageBox.Show("http streams support mp3 only for now and YOUTUBE NO LONGER PROVIDES MP3's through their android player api", "Error");
                    return;
                }
                if (MessageBox.Show("http streams support mp3 only for now and are really broken, do you want to continue", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Player = new StreamWavePlayer();
                    Player.LoadFile(textBox1.Text);
                    Player.Play();
                    decoderLabel.Text = "Decoder: StreamWavePlayer (MP3 STREAM PLAYER)";
                    ChannelsLabel.Text = $"Channels: {Player.ChannelCount()}";
                    BPSLabel.Text = $"Bits per sample: {Player.GetBitsPerSample()}";
                    SampleRateLabel.Text = $"Sample rate: {Player.GetSampleRate()}Hz";
                    Player.TrackEnd += OutputDevice_PlaybackStopped;
                    return;
                }
                else
                {
                    return;
                }
            }

            string? fileExt = Path.GetExtension(textBox1.Text).ToLowerInvariant();
            bool usingcustom = false;
            if (keyValuePairs.ContainsKey(fileExt[1..]))
            {
                (string decoderfile, string nameofdecoder) = keyValuePairs[fileExt[1..]];
                Assembly dll = Assembly.LoadFile(Path.GetFullPath(decoderfile, System.AppContext.BaseDirectory));
                if (dll != null)
                {
                    Type? type = dll.GetType(nameofdecoder);
                    if (type != null)
                    {
                        if (type.GetInterface(nameof(IPlay)) != null)
                        {
                            Player = (IPlay?)Activator.CreateInstance(type, textBox1.Text);
                        }
                        else if (type.GetInterface(nameof(IWaveProvider)) != null)
                        {
                            Player = new WaveFilePlayer();
                            ((WaveFilePlayer)Player).LoadFromProvider((IWaveProvider?)Activator.CreateInstance(type, textBox1.Text));
                        }
                    }
                }
                usingcustom = Player != null;
                decoderLabel.Text = $"Decoder: {dll?.GetType(nameofdecoder)?.FullName} ({fileExt}) (Custom)";
            }
            switch (fileExt)
            {
                case ".mid":
                case ".midi":
                    {
                        Player = new MidiPlayer(selectedmidiout);
                        Player.LoadFile(textBox1.Text);
                        decoderLabel.Text = "Decoder: MidiPlayer";
                        usingcustom = true;
                        break;
                    }
                case "opus":
                    {
                        MessageBox.Show("Opus is not supported", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
            }

            if (!usingcustom)
            {
                Player = new WaveFilePlayer();
                Player.LoadFile(textBox1.Text);
                //decoderLabel.Text = "Decoder: " + ((WaveFilePlayer)Player).Decoder;
            }
            */
            if (play)
            {
                Player.Play();
                Player.TrackEnd += OutputDevice_PlaybackStopped;
                /*ChannelsLabel.Text = $"Channels: {Player.ChannelCount()}";
                BPSLabel.Text = $"Bits per sample: {Player.GetBitsPerSample()}";
                SampleRateLabel.Text = $"Sample rate: {Player.GetSampleRate()}Hz";

                tracktime.Text = Player.Length()?.ToString("g");
                */
                Player?.SetVolume((byte)volumeBar.Value);
                ProgressBar.Pos = TimeSpan.FromMilliseconds(0);
                token = new();
                th = new Thread(() => SndThrd(token.Token));
                Player.TrackEnd += (s, e) => token.Cancel();
                var total = Player?.Length();
                if (total != null)
                {
                    ProgressBar.Max = (TimeSpan)total;
                }
                th.Start();
                //Track theTrack = new(textBox1.Text);
                /*var img = theTrack.EmbeddedPictures.FirstOrDefault();
                if (img != null)
                {
                    metadataImage.Image = Image.FromStream(new MemoryStream(img.PictureData));
                }
                else
                {
                    metadataImage.Image = null;
                }
                preventstoppedstatus = true;*/
                // TrackLmao(TrackEvent.ChangeSong);
            }
            if (resetsal)
            {
                StopAutoLoading = false;
            }
        }

        private CancellationTokenSource? token = new();

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
                    ProgressBar.Invoke(m);
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
            Player?.Stop();
        }

        private void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Debug.WriteLine("Output device playback stopped");
            //Debug.WriteLine("Loop single checked: " + ls.Checked);
            Debug.WriteLine("StopAutoLoading: " + StopAutoLoading);
            Debug.WriteLine("Current song: " + CurrentSong);
            /*if (ls.Checked && !StopAutoLoading)
            {
                RemoveTrack();
                StartPlaying();
            }
            else*/
            if (CurrentSong == null && !StopAutoLoading)
            {
                TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
                treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
                int index = aa.First(x => (Song)x.Tag == CurrentSong).Index;
                if (index + 1 < aa.Length)
                {
                    HandleSongChanging((Guid)aa[index + 1].Tag);
                }
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

        private void HandleSongChanging(Guid NextSong)
        {
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            Debug.WriteLine("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            int index = aa.FirstOrDefault(x => (Song)x.Tag == CurrentSong)?.Index ?? 0;
            var nxt = aa.First(x => ((Song)x.Tag).Guid == NextSong);
            int indexNext = nxt.Index;
            CurrentSong = (Song)nxt.Tag;
            aa[index].ForeColor = aa[indexNext].ForeColor;
            aa[indexNext].ForeColor = Color.LightGreen;
            RemoveTrack();
            StartPlaying();
        }

        private void DragOverM(object sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            //var data = (TreeNode?)e.Data?.GetData("System.Windows.Forms.TreeNode");
            /*if (SourceNode != null)
            {
                e.Effect = DragDropEffects.Move;
            }
            else if (data != null)
            {
                SourceNode = data;
                e.Effect = DragDropEffects.Move;
            }*/
        }

        private void DragDropOp(object sender, DragEventArgs e)
        {
            if (stateofdoingstuff != 0)
            {
                return;
            }
            string[]? files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1 && Directory.Exists(files[0]))
            {
                files = Directory.GetFiles(files[0]).Where(x => !x.EndsWith(".png") && !x.EndsWith(".txt") && !x.EndsWith(".pdf") && !x.EndsWith(".jpg") && !x.EndsWith(".lnk") && !x.EndsWith(".md") && !x.EndsWith(".zip") && !x.EndsWith(".7z") && !x.EndsWith(".rar") && !x.EndsWith(".exe") && !x.EndsWith(".dll") && !x.EndsWith(".json") && !x.EndsWith(".toml") && !x.EndsWith(".yaml") && !x.EndsWith(".xml") && !x.EndsWith(".nfo") && !x.EndsWith(".html") && !x.EndsWith(".m3u") && !x.EndsWith(".xmp") && !x.EndsWith(".log") && !x.EndsWith(".gif")).ToArray();
            }
            if ((files.Length > 1) || files.Length == 1 && File.Exists(files[0]))
            {
                int a = treeView1.Nodes[0].Nodes.Count;
                treeView1.Nodes[0].Nodes.AddRange(files.Select(x => new TreeNode(x) { Tag = new Song(x, x, Guid.NewGuid()) }).ToArray());
                treeView1.ExpandAll();
                if (ShouldPlayAfterSelect() && (a == 0 || MessageBox.Show("Would you like to skip to the newly added part?", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes))
                {
                    CurrentSong = (Song)treeView1.Nodes[0].Nodes[a].Tag;
                    Debug.WriteLine("StopAutoLoading set to true in form1dragdrop");
                    StopAutoLoading = true;
                    RemoveTrack();
                    StartPlaying();
                    Debug.WriteLine("StopAutoLoading set to false in form1dragdrop");
                    StopAutoLoading = false;
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
                    int sourceIndex = SourceNode.Index;
                    int destIndex = DestinationNode.Index;
                    if (SourceNode.Level == 1 && DestinationNode.Level == 1)
                    {
                        if (sourceIndex < destIndex)
                        {
                            TreeNode parentNode = SourceNode.Parent;
                            SourceNode.Remove();
                            DestinationNode.Remove();
                            parentNode.Nodes.Insert(sourceIndex, DestinationNode);
                            parentNode.Nodes.Insert(destIndex, SourceNode);
                        }
                        else
                        {
                            TreeNode parentNode = SourceNode.Parent;
                            SourceNode.Remove();
                            DestinationNode.Remove();
                            parentNode.Nodes.Insert(sourceIndex - 1, DestinationNode);
                            parentNode.Nodes.Insert(destIndex, SourceNode);
                        }
                    }
                    else if (SourceNode.Level == 2 && DestinationNode.Level == 2)
                    {
                        if (SourceNode.Parent == DestinationNode.Parent)
                        {
                            if (sourceIndex < destIndex)
                            {
                                TreeNode parentNode = SourceNode.Parent;
                                SourceNode.Remove();
                                DestinationNode.Remove();
                                parentNode.Nodes.Insert(sourceIndex, DestinationNode);
                                parentNode.Nodes.Insert(destIndex, SourceNode);
                            }
                            else
                            {
                                TreeNode parentNode = SourceNode.Parent;
                                SourceNode.Remove();
                                DestinationNode.Remove();
                                parentNode.Nodes.Insert(sourceIndex - 1, DestinationNode);
                                parentNode.Nodes.Insert(destIndex, SourceNode);
                            }
                        }
                        else
                        {
                            TreeNode addNode = DestinationNode.Parent;
                            SourceNode.Remove();
                            addNode.Nodes.Insert(destIndex, SourceNode);
                        }
                    }
                    else if (SourceNode.Level == 2 && DestinationNode.Level == 1)
                    {
                        if (DestinationNode.Nodes.Count == 0)
                        {
                            SourceNode.Remove();
                            DestinationNode.Nodes.Insert(0, SourceNode);
                        }
                        else

                        {
                            int i = DestinationNode.LastNode.Index;
                            SourceNode.Remove();
                            DestinationNode.Nodes.Insert(i + 1, SourceNode);
                        }
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
            if (e.Button == MouseButtons.Right && e.Node.Tag == null)
            {
                contextMenuStrip2.Show(treeView1, new Point(e.X, e.Y));
            }
            else if (e.Button == MouseButtons.Right && e.Node.Tag != null)
            {
                SelectedItem = e.Node;
                contextMenuStrip1.Show(treeView1, new Point(e.X, e.Y));
            }
            treeView1.SelectedNode = e.Node;
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Node.Tag != null)
            {
                Debug.WriteLine("StopAutoLoading set to true in treeView1_NodeMouseDoubleClick");
                StopAutoLoading = true;
                HandleSongChanging(((Song)e.Node.Tag).Guid);
            }
        }

        private void playNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedItem != null)
            {
                Debug.WriteLine("StopAutoLoading set to true in playNowToolStripMenuItem_Click");
                StopAutoLoading = true;
                HandleSongChanging(((Song)SelectedItem.Tag).Guid);
            }
        }

        private void upToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            int index = aa.First(x => x == SelectedItem).Index;
            if (index > 0)
            {
                treeView1.Nodes[0].Nodes.RemoveAt(index);
                treeView1.Nodes[0].Nodes.Insert(index - 1, aa[index]);
                treeView1.SelectedNode = aa[index];
            }
        }

        private void downToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            int index = aa.First(x => x == SelectedItem).Index;
            if (index < aa.Length)
            {
                treeView1.Nodes[0].Nodes.RemoveAt(index);
                treeView1.Nodes[0].Nodes.Insert(index + 1, aa[index]);
                treeView1.SelectedNode = aa[index];
            }
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
            TreeNode[]? aa = new TreeNode[treeView1.Nodes[0].Nodes.Count];
            treeView1.Nodes[0].Nodes.CopyTo(aa, 0);
            treeView1.Nodes[0].Nodes.Remove(aa.First(x => x == SelectedItem));
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
                if (lines.Length > 1)
                {
                    int a = treeView1.Nodes[0].Nodes.Count;
                    treeView1.Nodes[0].Nodes.AddRange(lines.Select(x => new TreeNode(x) { Tag = Guid.NewGuid() }).ToArray());
                    treeView1.ExpandAll();
                    if (ShouldPlayAfterSelect() && (a == 0 || MessageBox.Show("Would you like to skip to the newly added part?", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes))
                    {
                        CurrentSong = (Song)treeView1.Nodes[0].Nodes[a].Tag;
                        if (Player != null)
                        {
                            Player.TrackEnd -= OutputDevice_PlaybackStopped;
                        }
                        RemoveTrack();
                        StartPlaying();
                    }
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
            Point cp = ProgressBar.ProgressBar.PointToClient(Cursor.Position);
            var a = ProgressBar.Min + ((ProgressBar.Max - ProgressBar.Min) * ((ulong)cp.X) / ((ulong)ProgressBar.ProgressBar.Width));
            ProgressBar.Pos = a;
            Player?.SetPosition(a);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            token.Cancel();
        }
    }
}
using Fluent;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Winforms;
using SilverConfig;
using SilverFormsUtils;
using Swordfish.NET.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Color = System.Drawing.Color;

namespace SilverAudioPlayer;

public partial class Form1 : Form
{
    private const string ConfigFileName = "settings\\silveraudioplayer.winforms.preferences.xml";

    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

    public const int HWND_BROADCAST = 0xffff;
    public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_AUDIOPLAYERZ");

    private System.Timers.Timer AutosaveConfig = new();
    private FileSystemWatcher cfw;
    private Preferences Config;
    private readonly string ConfigLoc;
    private IConfigReader<Preferences> ConfigReader;


    private readonly MainWindowContext ctx;
    private string[]? filetodrop;

    private bool HotKeyRegistered = false;

    private MetadataForm mf;
    private bool savednow = false;


    private TreeNode SelectedItem;


    private TreeNode? SourceNode;
    private byte stateofdoingstuff;
    private bool StopAutoLoading = false;
    private Thread? th;

    private CancellationTokenSource? token = new();
    private readonly bool WatchForConfigChanges = true;

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

        ctx = new MainWindowContext(this)
        {
            ResetUIScrollBar = () =>
            {
                token = new();
                th = new Thread(() => SndThrd(token.Token));
            },
            SetScrollBarTextTo = scrl => Invoke(() => ProgressBar.Max = scrl),
            HandleLateStageMetadataAndScrollBar = () =>
            {
                Invoke(() => { ProgressBar.Pos = TimeSpan.Zero; });
                th.Start();

                if (CurrentSong?.Metadata != null)
                {
                    if (CurrentSong.Metadata.Title != null)
                        Invoke(() => base.Text = CurrentSong.Metadata.Title + " - SilverAudioPlayer");
                    if (CurrentSong?.Metadata?.Pictures?.Any() == true)
                    {
                        var buffer = CurrentSong.Metadata.Pictures[0].Data;
                        if (buffer != null)
                        {
                            using var memstream = new MemoryStream(buffer);
                            Invoke(() => pictureBox1.Image = Image.FromStream(memstream));
                        }
                    }
                    else
                    {
                        Invoke(() => pictureBox1.Image = null);
                    }
                }
            },
            ShowMessageBox = (s, s1) => {
                MessageBox.Show(s1, s);
            }
        };
        Logic = new Logic<MainWindowContext>(ctx);
        ProgressBar.ProgressBar.MouseClick += ProgressBar_MouseClick;
        OnConfigChange(true);
        AutosaveConfig.Elapsed += AutosaveConfig_Elapsed;
        AutosaveConfig.Start();
        if (WatchForConfigChanges)
        {
            cfw = new(Path.GetDirectoryName(ConfigLoc));
            cfw.Changed += Cfw_OnChangedE;
            cfw.Deleted += Cfw_OnDeletedE;
            cfw.Created += Cfw_OnCreatedE;
            cfw.Renamed += Cfw_OnRenamedE;
        }

        if (GetDarkModePreference.ShouldIUseDarkMode())
        {
            this.UseDarkModeBar(true);
            this.UseDarkModeForThingsInsideOfForm(true, true);
        }

        // display the the file name as the list item label
        list.Properties.Name = "Metadata.TrackNumber";

        // display some more properties from our objects as columns
        list.ShowColumns = true;
        list.Properties.Columns = new List<string> { "TitleOrURLF", "Metadata.Artist", "Metadata.Album" };
        list.Properties.ColumnNames = new List<string> { "Name", "Artist", "Album" };

        list.ContextMenuStrip = contextMenuStrip1;
        /*list.MouseClick += (x, y) => { 
        if(y.Button == MouseButtons.Right)
            {
                switch (list.SelectedItem)
                {
                    case null:
                        contextMenuStrip2.Show(list, new Point(y.X, y.Y));
                        break;

                    default:
                        contextMenuStrip1.Show(list, new Point(y.X, y.Y));
                        break;
                }
            }
        };*/

        // set the default items

        // list.Theme = OLVTheme.VistaExplorer;
        list.ItemFont = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        list.EnableDropFiles = true;
        list.EnableDropOnLocations = DropTargetLocation.Background | DropTargetLocation.Item;

        list.OnDroppedFiles = (List<string> paths) =>
        {
            Logic.ProcessFiles(Logic.FilterFiles(paths));
            list.Redraw();
        };
        list.OnItemDoubleClick = x => { Logic.HandleSongChanging((Song)x); };
        list.Items = ctx.Queue.ToList();
        // list.InnerList.BeforeSorting += (x, y) => { y.Canceled = true; };
        /*list.InnerList.CustomSorter = (x, y) =>
        {
            list.InnerList.ListViewItemSorter = new ColumnComparer(
          new("ignored", "Metadata.TrackNumber"), SortOrder.Ascending);
        };*/
        list.InnerList.UseNotifyPropertyChanged = true;
        list.InnerList.SetObjects(ctx.Queue);
        list.InnerList.Sort(1);
        list.InnerList.Columns[0].Text = "Track #";

        list.Redraw();
        list.InnerList.DragSource = new SimpleDragSource();
        list.InnerList.DropSink = new RearrangingDropSink(false);
        ctx.ObservableForProperty(x => x.Queue, skipInitial: false).Subscribe(x =>
        {
            list.InnerList.SetObjects(ctx.Queue);
             ((ConcurrentObservableCollection<Song>)x.Value).CollectionChanged += (x, y) =>
            {
                Invoke(() =>
                {
                    list.InnerList.Sort(0);
                    list.Redraw();
                });
            };
        });
        if (Logic.MusicStatusInterfaces?.Any() == true)
            Parallel.ForEach(Logic.MusicStatusInterfaces, dangthing =>
            {
                var a = dangthing;
                GC.KeepAlive(a);
                Logic.AddMSI(a);
            });
    }

    public Form1(params string[] files) : this()
    {
        Logic.ProcessFiles(files);
    }

    private string? CurrentURI => CurrentSong?.URI;

    private Song? CurrentSong
    {
        get => Logic.CurrentSong;
        set => Logic.CurrentSong = value;
    }

    public IPlay? Player => Logic.Player;

    public Logic<MainWindowContext> Logic { get; set; }

    private Song? NextSong
    {
        get => Logic.NextSong;
        set => Logic.NextSong = value;
    }

    private void Cfw_OnRenamedE(object? sender, RenamedEventArgs e)
    {
    }

    private void Cfw_OnCreatedE(object? sender, FileSystemEventArgs e)
    {
    }

    private void Cfw_OnDeletedE(object? sender, FileSystemEventArgs e)
    {
        Config = new Preferences();
        ConfigReader.Write(Config, ConfigLoc);
    }

    private void AutosaveConfig_Elapsed(object? sender, ElapsedEventArgs e)
    {
        AutoSaveConfig();
    }

    private void Cfw_OnChangedE(object? sender, FileSystemEventArgs e)
    {
        if (savednow)
        {
            savednow = false;
        }
        else
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, ConfigFileName)))
            {
                Config = ConfigReader.Read(ConfigLoc);
            }
            else
            {
                Config = new Preferences();
                ConfigReader.Write(Config, ConfigLoc);
            }

            OnConfigChange();
        }
    }

    private void AutoSaveConfig()
    {
        savednow = true;
        Log.Information("Saving config");
        volumeBar.Invoke(() => Config.Volume = (byte)volumeBar.Value);
        Config.ProgressBarRainbow = ProgressBar.ProgressBar.Rainbow;
        Config.ProgressBarRainbowShift = ProgressBar.ProgressBar.ShiftRainbow;
        Config.ProgressBarRainbowCaching = (byte)ProgressBar.ProgressBar.CacheRainbowDecimals;
        Config.ProgressBarColour = ProgressBar.ProgressBar.Color.ToArgb();
        Config.MillisecondIntervalOfAutoSave = (ulong)AutosaveConfig.Interval;
        ConfigReader.Write(Config, ConfigLoc);
        Log.Information("Config saved");
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
            if (Config.Volume > 100) Config.Volume = 100;
            Player?.SetVolume(Config.Volume);
        }
        else
        {
            volumeBar.Invoke(() => volumeBar.Value = Config.Volume > 100 ? 100 : Config.Volume);
            if (Config.Volume > 100) Config.Volume = 100;
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
        else if ((!Config!.ProcessMessages || !Config.HandleMediaControls) && HotKeyRegistered)
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

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);

    private void ShowMe()
    {
        if (Config.AutoMagicallyLoadFromArgstxt ||
            MessageBox.Show("Would you like to load the files you tried to load in a different instance?", "Question",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "args.txt");

            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                Logic.ProcessFiles(lines);
            }
        }

        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
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
            if (m.Msg == WM_SHOWME) ShowMe();
            if (Config.HandleMediaControls)
                switch (m.Msg)
                {
                    case WM_APPCOMMAND:
                        int cmd = ((int)m.LParam >> 16) & 0xFF;
                        switch (cmd)
                        {
                            case APPCOMMAND_MEDIA_PLAY_PAUSE:
                                Logic.PlayPause(true);

                                break;
                        }

                        m.Result = (IntPtr)1;
                        break;

                    case 0x0312:
                        switch (m.WParam.ToInt32())
                        {
                            case 1:
                                Logic.Play();
                                break;

                            case 2:
                                Logic.Pause();
                                break;

                            case 3:
                                Logic.PlayPause(true);
                                break;
                        }

                        break;
                }
        }

        base.WndProc(ref m);
    }


    private void SndThrd(CancellationToken e)
    {
        MethodInvoker m = new(() => ProgressBar.Pos = Player?.GetPosition() ?? TimeSpan.FromMilliseconds(2));
        while (ProgressBar.Pos < ProgressBar.Max)
            if (Player?.GetPlaybackState() == PlaybackState.Playing)
            {
                if (e.IsCancellationRequested || !Visible) return;
                try
                {
                    ProgressBar.Invoke(m);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
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

    public void RemoveTrack()
    {
        Text = "SilverAudioPlayer";
        Player?.Stop();
    }


    private void DragOverM(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }

    private IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return Logic.FilterFiles(files);
    }

    private void DragDropOp(object sender, DragEventArgs e)
    {
        if (stateofdoingstuff != 0) return;
        string[]? files = null;
        if (e.Data?.GetDataPresent("UniformResourceLocatorW") == true)
            files = new string[]
            {
                System.Text.Encoding.Unicode.GetString(
                    ((MemoryStream)e.Data.GetData("UniformResourceLocatorW")).ToArray())
            };
        else if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            files = (string[])e.Data.GetData(DataFormats.FileDrop);
        Logic.ProcessFiles(files);
    }

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
        if ((e.Data?.GetDataPresent(DataFormats.FileDrop) == true ||
             e.Data?.GetDataPresent("UniformResourceLocatorW") == true) && SourceNode == null)
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
                Log.Debug(lstnod.Bounds.X + " " + lstnod.Bounds.Y);
                if (pt.Y > lstnod.Bounds.Y && pt.X > lstnod.Bounds.X && pt.X < lstnod.Bounds.X + lstnod.Bounds.Right)
                    DestinationNode = lstnod;
            }
        }

        if (DestinationNode == null) return;
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
                Log.Error(ex.Message);
            }

            SourceNode = null;
        }

        stateofdoingstuff = 0;
    }


    private void volumeBar_Scroll(object sender, EventArgs e)
    {
        Player?.SetVolume((byte)volumeBar.Value);
    }

    private void PlayButton_Click(object sender, EventArgs e)
    {
        Logic.Play();
    }

    private void PauseButton_Click(object sender, EventArgs e)
    {
        Logic.Pause();
    }


    private void playNowToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (SelectedItem != null) Logic.HandleSongChanging((Song)SelectedItem.Tag);
    }

    private void upToolStripMenuItem_Click(object sender, EventArgs e)
    {
        MoveSel(-1);
    }

    private void downToolStripMenuItem_Click(object sender, EventArgs e)
    {
        MoveSel(+1);
    }

    private void treeView1_MouseLeave(object sender, EventArgs e)
    {
        if (filetodrop != null)
            /*treeView1.DoDragDrop(new DataObject(DataFormats.FileDrop, filetodrop), DragDropEffects.Copy |
DragDropEffects.Move);*/
            filetodrop = null;
    }

    private void treeView1_MouseUp(object sender, MouseEventArgs e)
    {
        filetodrop = null;
    }

    private void treeView1_MouseDown(object sender, MouseEventArgs e)
    {
        /*if (treeView1.SelectedNode != null && e.Button == MouseButtons.Left)
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
        }*/
    }

    private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        /* if (e.Button == MouseButtons.Right)
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
         treeView1.SelectedNode = e.Node;*/
    }

    private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        /* if (e.Button == MouseButtons.Left && e.Node.Tag != null)
         {
             Logic.HandleSongChanging((Song)e.Node.Tag);
         }*/
    }

    private void MoveSel(short where)
    {
        //  MoveTrack(where, SelectedItem);
    }

    private void MoveTrack(short howmuch, TreeNode what)
    {
        //treeView1.Nodes[0].Nodes.RemoveAt(what.Index);
        //treeView1.Nodes[0].Nodes.Insert(what.Index + howmuch, what);
    }

    private void playNextToolStripMenuItem_Click(object sender, EventArgs e)
    {
        NextSong = (Song)list.SelectedItem;
    }

    private void removeFromQueueToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (list.SelectedItem != null) ctx.Queue.Remove((Song)list.SelectedItem);
    }

    private void clearQueueToolStripMenuItem_Click(object sender, EventArgs e)
    {
        /*treeView1.Nodes[0].Nodes.Clear();*/
        ctx.Queue.Clear();
        CurrentSong = null;
    }

    private void importFromClipboardToolStripMenuItem_Click(object sender, EventArgs e)
    {
        string? text = Clipboard.GetText();
        if (!string.IsNullOrEmpty(text))
        {
            string[]? lines = text.Split(Environment.NewLine);
            if (lines != null) Logic.ProcessFiles(lines);
        }
    }

    private void exportToClipboardnewLineSeperatedToolStripMenuItem_Click(object sender, EventArgs e)
    {
        StringBuilder? sb = new();
        foreach (Song a in ctx.Queue) sb.AppendLine(a.URI);
        Clipboard.SetText(sb.ToString());
    }

    private void ProgressBar_MouseClick(object sender, MouseEventArgs e)
    {
        Point seekpoint = ProgressBar.ProgressBar.PointToClient(Cursor.Position);
        var a = ProgressBar.Min + (ProgressBar.Max - ProgressBar.Min) * (ulong)seekpoint.X /
            (ulong)ProgressBar.ProgressBar.Width;
        ProgressBar.Pos = a;
        Player?.SetPosition(a);
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        cfw.Dispose();
        AutoSaveConfig();
        AutosaveConfig.Stop();
        AutosaveConfig.Dispose();
        while (Logic.musicStatusInterfaces.Count != 0) Logic.RemoveMSI(Logic.musicStatusInterfaces[0]);
        if (Player != null)
        {
            // Player.TrackEnd -= OutputDevice_PlaybackStopped;
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
    }

    private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
    {
        DoDragDrop(e.Item, DragDropEffects.Move);
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        if (Logic.MusicStatusInterfaces?.Any() == true)
            Parallel.ForEach(Logic.MusicStatusInterfaces, dangthing =>
            {
                var a = dangthing;
                GC.KeepAlive(a);
                Logic.AddMSI(a);
            });
    }

    private void pictureBox1_Click(object sender, EventArgs e)
    {
        if (CurrentSong != null)
        {
            if (mf != null)
            {
                mf.Close();
                mf.Dispose();
            }

            mf = new MetadataForm(ctx.CurrentSong);
            mf.Show();
        }
    }

    private void Form1_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F1)
        {
            Manual m = new(Logic);
            m.Show();
        }
    }
}

public class MainWindowContext : PlayerContext
{
    private readonly Form1 mainWindow;

    public MainWindowContext(Form1 mw)
    {
        mainWindow = mw ?? throw new ArgumentNullException(nameof(mw));
    }
}
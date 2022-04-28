using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.CAD;

[Export(typeof(IMusicStatusInterface))]
public class CADMusicStatusInterface : Form, IMusicStatusInterface
{
    public CADMusicStatusInterface()
    {
        InitializeComponent();
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
    }

    private Label label1;
    private bool disposedValue;

    [DllImport("KERNEL32", EntryPoint = "RtlMoveMemory")]
    private static extern void CopyMemory(object hpvDest, object hpvSource, long cbCopy);

    [DllImport("user32", EntryPoint = "FindWindowA")]
    private static extern long FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32", EntryPoint = "SendMessageA")]
    private static extern long SendMessage(long hwnd, long wMsg, long wParam, CopyDataStruct lParam);

    [DllImport("user32", EntryPoint = "SendMessageA")]
    private static extern long SendMessage(long hwnd, long wMsg, long wParam, long lParam);

    [DllImport("user32", EntryPoint = "CallWindowProcA")]
    private static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, long msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32", EntryPoint = "PostMessageA")]
    private static extern long PostMessage(long WndID, long wMsg, long wParam, long lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
    private static extern unsafe void MoveMemory(void* dest, void* src, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyDataStruct
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    public void cadRegister()
    {
        long ThWnd;
        string strTemp;
        ThWnd = FindWindow(null, "CD Art Display 1.x Class");
        Debug.WriteLine(ThWnd);

        strTemp = "1" + "\t" + "\t" + "SilverAudioPlayerIPC" + "\t" + AppContext.BaseDirectory + @"SilverAudioPlayer.Winforms.exe" + "\t";
        CopyDataStruct copyData = new();
        copyData.lpData = Marshal.StringToHGlobalUni(strTemp);
        if (copyData.lpData != IntPtr.Zero)
        {
            copyData.dwData = new IntPtr(700);
            copyData.cbData = System.Text.Encoding.Unicode.GetByteCount(strTemp);
            IntPtr copyDataBuff = Marshal.AllocHGlobal(Marshal.SizeOf(copyData));
            if (copyDataBuff != IntPtr.Zero)
            {
                Marshal.StructureToPtr(copyData, copyDataBuff, false);
                var e = SendMessage(ThWnd, WM_COPYDATA, 700, copyData);
                Debug.WriteLine(e);
                Marshal.FreeHGlobal(copyDataBuff);
            }
            Marshal.FreeHGlobal(copyData.lpData);
        }
    }

    private IntPtr PrevProc;
    private const int GWL_WNDPROC = -4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    private static IntPtr SetWindowProc(IntPtr hWnd, WndProcDelegate newWndProc)
    {
        IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
        IntPtr oldWndProcPtr;

        if (IntPtr.Size == 4)
            oldWndProcPtr = SetWindowLongPtr32(hWnd, GWL_WNDPROC, newWndProcPtr);
        else
            oldWndProcPtr = SetWindowLongPtr64(hWnd, GWL_WNDPROC, newWndProcPtr);

        return oldWndProcPtr;
    }

    public void HookMsg(IntPtr hwnd)
    {
        WndProcDelegate handler = CallbackMsgs;
        Handles.Add(GCHandle.Alloc(handler));

        PrevProc = SetWindowProc(hwnd, handler);
        Handles.Add(GCHandle.Alloc(PrevProc));
    }

    private const int WM_USER = 0x400;

    private string ReplaceNullWithEmptyAndTabsWithSpace(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }
        return input.Replace('\t', ' ');
    }

    public void cadSendSongInfo()
    {
        CopyDataStruct cdCopyData;
        string strTemp;
        string arglpWindowName = "CD Art Display 1.x Class";
        Song? song = GetCurrentTrack != null ? GetCurrentTrack() : null;
        if (song == null)
        {
            return;
        }
        const string vbTab = "\t";
        var ThWnd = FindWindow(null, arglpWindowName);
        if (song.Metadata != null)
        {
            string art = song.URI;
            if (song.Metadata.Pictures != null && song.Metadata.Pictures.Any())
            {
                Picture SelectBest(IReadOnlyList<Picture> pictures)
                {
                    return pictures.First(x => x != null);
                }
                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Art")))
                {
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Art"));
                }
                var bestart = SelectBest(song.Metadata.Pictures);

                if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Art", bestart.Hash + ".jpg")))
                {
                    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Art", bestart.Hash + ".jpg"), bestart.Data);
                }
                art = Path.Combine(AppContext.BaseDirectory, "Art", bestart.Hash + ".jpg");
            }
            strTemp = $"{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Title)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Artist)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Album)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Genre)}\t{song.Metadata.Year}\t\t{(song.Metadata.TrackNumber ?? 0)}\t{((int)(song.Metadata.Duration / 1000 ?? 60))}\t{song.URI.Replace("\t", "\\t")}\t0\t{art.Replace("\t", "\\t")}\t\t\t\t\t\t\t";
            Debug.WriteLine(strTemp);
        }
        else

        {
            strTemp = song.Name + vbTab + "Song Artist" + vbTab + "Song Album" + vbTab + "Song Genre" + vbTab + "Year" + vbTab + "Comments" + vbTab + "1" + vbTab + "240" + vbTab + song.URI + vbTab + "5" + vbTab + song.URI + vbTab + "Composer" + vbTab + "128";
        }

        cdCopyData.dwData = new IntPtr(701);
        cdCopyData.cbData = System.Text.Encoding.Unicode.GetByteCount(strTemp);
        cdCopyData.lpData = Marshal.StringToHGlobalUni(strTemp);
        SendMessage(ThWnd, WM_COPYDATA, 701, cdCopyData);
    }

    public IntPtr CallbackMsgs(IntPtr Hwnd, int wMsg, IntPtr wparam, IntPtr lparam)
    {
        long wp_id = wparam.ToInt64();
        long lp_id = lparam.ToInt64();
        //long wHwnd = Hwnd.ToInt64();
        long CallbackMsgsRet = default;
        Debug.WriteLine("wp " + wp_id + " lp " + lp_id);
        switch (wMsg)
        {
            case WM_USER:
                {
                    if (lp_id == 128)
                    {
                        SetRepeat?.Invoke(this, wp_id == 1 ? RepeatState.One : RepeatState.None);
                        Debug.WriteLine("Repeat: " + wp_id);
                    }

                    if (lp_id == 141)
                    {
                        Debug.WriteLine("Shuffle: " + wp_id);
                        SetShuffle?.Invoke(this, wp_id == 1);
                    }
                    if (lp_id == 114)
                    {
                        Debug.WriteLine("setting song position?");
                        SetPosition?.Invoke(this, (ulong)wp_id);
                        return new IntPtr(CallbackMsgsRet);
                    }
                    if (lp_id == 108)
                    {
                        Debug.WriteLine("Volume " + wp_id);
                        SetVolume?.Invoke(this, (byte)wp_id);
                    }
                    if (lp_id == 639)
                    {
                        void SetRatingI(byte val)
                        {
                            Debug.WriteLine("Rating set in CAD: " + val);
                            SetRating?.Invoke(this, val);
                        }
                        switch (wp_id)
                        {
                            case 0:
                                {
                                    SetRatingI(0);
                                    break;
                                }
                            case 1:
                            case 2:
                                {
                                    SetRatingI(1);
                                    break;
                                }
                            case 3:
                            case 4:
                                {
                                    SetRatingI(2);
                                    break;
                                }
                            case 5:
                            case 6:
                                {
                                    SetRatingI(3);
                                    break;
                                }
                            case 7:
                            case 8:
                                {
                                    SetRatingI(4);
                                    break;
                                }
                            case 9:
                            case 10:
                                {
                                    SetRatingI(5);
                                    break;
                                }
                        }
                    }

                    switch (wp_id)
                    {
                        case 0:
                            {
                                switch (lp_id)
                                {
                                    case 101:
                                        {
                                            Debug.WriteLine("Play/Pause");
                                            PlayPause?.Invoke(this, EventArgs.Empty);
                                            break;
                                        }

                                    case 103:
                                        {
                                            Debug.WriteLine("Stop");
                                            Stop?.Invoke(this, EventArgs.Empty);
                                            break;
                                        }

                                    case 104:
                                        {
                                            Debug.WriteLine("Next");
                                            Next?.Invoke(this, EventArgs.Empty);
                                            if (GetCurrentTrack != null && TrackChangedNotification != null)
                                            {
                                                TrackChangedNotification(GetCurrentTrack());
                                            }
                                            break;
                                        }

                                    case 105:
                                        {
                                            Debug.WriteLine("Prev");
                                            Previous?.Invoke(this, EventArgs.Empty);
                                            if (GetCurrentTrack != null && TrackChangedNotification != null)
                                            {
                                                TrackChangedNotification(GetCurrentTrack());
                                            }
                                            break;
                                        }

                                    case 109:
                                        {
                                            Debug.WriteLine("returning volume?");
                                            if (GetVolume != null)
                                            {
                                                CallbackMsgsRet = GetVolume();
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 70;
                                            }
                                            return new IntPtr(CallbackMsgsRet);
                                        }

                                    case 110:
                                        {
                                            Debug.WriteLine("returning song?");
                                            cadSendSongInfo();
                                            CallbackMsgsRet = 1;
                                            return new(CallbackMsgsRet);
                                        }

                                    case 113:
                                        {
                                            Debug.WriteLine("returning song duration?");
                                            if (GetDuration != null)
                                            {
                                                CallbackMsgsRet = (long)GetDuration();
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 0;
                                            }
                                            return new(CallbackMsgsRet);
                                        }

                                    case 122:
                                        {
                                            Debug.WriteLine("returning song position?");
                                            if (GetPosition != null)
                                            {
                                                CallbackMsgsRet = (long)GetPosition();
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 0;
                                            }
                                            return new IntPtr(CallbackMsgsRet);
                                        }
                                    case 125:
                                        {
                                            Debug.WriteLine("returning PLAYER_STATE (code 125)");
                                            if (GetState != null)
                                            {
                                                CallbackMsgsRet = GetState() switch
                                                {
                                                    PlaybackState.Playing => 2,
                                                    PlaybackState.Paused => 1,
                                                    PlaybackState.Stopped => 0,
                                                    _ => 0
                                                };
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 0;
                                            }
                                            Debug.WriteLine("state is " + CallbackMsgsRet);
                                            return new IntPtr(CallbackMsgsRet);
                                        }

                                    case 130:
                                        {
                                            Debug.WriteLine("returning repeat status? (code 130)");
                                            if (GetRepeat != null)
                                            {
                                                CallbackMsgsRet = GetRepeat() switch
                                                {
                                                    RepeatState.None => 0,
                                                    RepeatState.One => 1,
                                                    RepeatState.Queue => 1,
                                                    _ => 0
                                                };
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 0;
                                            }

                                            return new IntPtr(CallbackMsgsRet);
                                        }

                                    case 140:
                                        {
                                            Debug.WriteLine("returning shuffle status? (code 140)");
                                            if (GetShuffle != null)
                                            {
                                                CallbackMsgsRet = GetShuffle() ? 1 : 0;
                                            }
                                            else
                                            {
                                                CallbackMsgsRet = 0;
                                            }
                                            return new IntPtr(CallbackMsgsRet);
                                        }

                                    case 801:
                                        {
                                            Debug.WriteLine("returning lytics status? (code 801)");
                                            cadSendLyrics();
                                            CallbackMsgsRet = 0;
                                            return new IntPtr(CallbackMsgsRet);
                                        }
                                }

                                break;
                            }
                    }

                    break;
                }

            case WM_COPYDATA:
                {
                    InterProcessComms(lparam);
                    break;
                }
        }

        CallbackMsgsRet = CallWindowProc(PrevProc, Hwnd, wMsg, wparam, lparam);
        return new(CallbackMsgsRet);
    }

    public void cadSendLyrics()
    {
        CopyDataStruct cdCopyData;
        string strTemp = "";
        string arglpWindowName = "CD Art Display 1.x Class";
        var ThWnd = FindWindow(null, arglpWindowName);
        var ct = GetCurrentTrack != null ? GetCurrentTrack() : null;
        if (ct != null && ct.Metadata != null && !string.IsNullOrEmpty(ct.Metadata.Lyrics))
        {
            strTemp = ct.Metadata.Lyrics;
        }
        cdCopyData.dwData = new IntPtr(702);
        cdCopyData.cbData = System.Text.Encoding.Unicode.GetByteCount(strTemp);
        cdCopyData.lpData = Marshal.StringToHGlobalUni(strTemp);
        SendMessage(ThWnd, WM_COPYDATA, 702, cdCopyData);
    }

    private void InterProcessComms(IntPtr lParam)
    {
        var cdCopyData = new CopyDataStruct();
        var bytebuffer = new byte[50000];
        string strTemp;
        string[] sInfo;
        CopyMemory(cdCopyData, lParam, Marshal.SizeOf(cdCopyData));
        if (cdCopyData.dwData == new IntPtr(110))
        {
            CopyMemory(bytebuffer[1], cdCopyData.lpData, cdCopyData.cbData);
            strTemp = System.Text.Encoding.Unicode.GetString(bytebuffer);
            strTemp = SLeft(strTemp, strTemp.IndexOf(strTemp, '\0') - 1);
            sInfo = strTemp.Split(Environment.NewLine);
        }
    }

    public static string SLeft(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        maxLength = Math.Abs(maxLength);

        return (value.Length <= maxLength
               ? value
               : value[..maxLength]
               );
    }

    private List<GCHandle> Handles = new();

    private const int WM_COPYDATA = 0x4A;

    public event EventHandler? Play;

    public event EventHandler? Pause;

    public event EventHandler? PlayPause;

    public event EventHandler? Stop;

    public event EventHandler? Next;

    public event EventHandler? Previous;

    public event EventHandler<byte>? SetVolume;

    public event EventHandler<ulong>? SetPosition;

    public event EventHandler<RepeatState>? SetRepeat;

    public event EventHandler<bool>? SetShuffle;

    public event EventHandler<byte>? SetRating;

    public event Func<byte>? GetVolume;

    public event Func<Song>? GetCurrentTrack;

    public event Func<ulong>? GetDuration;

    public event Func<ulong>? GetPosition;

    public event Func<PlaybackState>? GetState;

    public event Func<RepeatState>? GetRepeat;

    public event Func<bool>? GetShuffle;

    public event Func<string>? GetLyrics;

    public event EventHandler<IMusicStatusInterface>? StateChangedNotification;

    public event EventHandler<IMusicStatusInterface>? RepeatChangedNotification;

    public event EventHandler<IMusicStatusInterface>? ShutdownNotiifcation;

    public event EventHandler<IMusicStatusInterface>? ShuffleChangedNotification;

    public event EventHandler<IMusicStatusInterface>? RatingChangedNotification;

    public event EventHandler<IMusicStatusInterface>? CurrentTrackNotification;

    public event EventHandler<IMusicStatusInterface>? CurrentLyricsNotification;

    public event EventHandler<IMusicStatusInterface>? NewLyricsNotification;

    public event EventHandler<IMusicStatusInterface>? NewCoverNotification;

    public void StartIPC()
    {
        Text = "SilverAudioPlayerIPC";
        Show();
        HookMsg(Handle);
        cadRegister();
    }

    public void StopIPC()
    {
        cadShutdown();
        label1.Invoke(() =>
        {
            Close();
            Dispose();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            foreach (var handle in Handles)
            {
                handle.Free();
            }
            disposedValue = true;
        }

        base.Dispose(disposing);
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~CADMusicStatusInterface()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void InitializeComponent()
    {
        this.label1 = new System.Windows.Forms.Label();
        this.SuspendLayout();
        //
        // label1
        //
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(12, 9);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(141, 15);
        this.label1.TabIndex = 0;
        this.label1.Text = "coh-myoo-nee-kay-shun";
        //
        // CADMusicStatusInterface
        //
        this.ClientSize = new System.Drawing.Size(162, 30);
        this.Controls.Add(this.label1);
        this.Name = "CADMusicStatusInterface";
        this.Load += new System.EventHandler(CADMusicStatusInterface_Load);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void CADMusicStatusInterface_Load(object sender, EventArgs e)
    {
        //Visible = false;
        //WindowState = FormWindowState.Minimized;
        //ShowInTaskbar = false;
    }

    public void TrackChangedNotification(Song? newtrack)
    {
        long ThWnd;
        ThWnd = FindWindow(null, "CD Art Display 1.x Class");
        Debug.WriteLine(ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 123);
        Debug.WriteLine(e);
    }

    public void PlayerStateChanged(PlaybackState newstate)
    {
        long ThWnd;
        ThWnd = FindWindow(null, "CD Art Display 1.x Class");
        Debug.WriteLine(ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 126);
        Debug.WriteLine(e);
    }

    public void cadShutdown()
    {
        long ThWnd;
        ThWnd = FindWindow(null, "CD Art Display 1.x Class");
        Debug.WriteLine(ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 129);
        Debug.WriteLine(e);
    }
}
using System.Composition;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.CAD;

[Export(typeof(IMusicStatusInterface))]
public class CADMusicStatusInterface : Form, IMusicStatusInterface
{
    private const int GWL_WNDPROC = -4;

    private const int WM_USER = 0x400;
    private const string arglpWindowName = "CD Art Display 1.x Class";

    private const int WM_COPYDATA = 0x4A;
    private bool disposedValue;

    private readonly List<GCHandle> Handles = new();

    private IntPtr PrevProc;
    public bool IsStarted => _IsStarted;
    private bool _IsStarted;
    public CADMusicStatusInterface()
    {
        InitializeComponent();
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        logger = Logger.GetLogger(typeof(CADMusicStatusInterface));
    }

    public ILogger? logger { get; set; }
    string ICodeInformation.Name => "CAD Music status interface";

    public string Description =>
        "CD Art display compatible interface, interfaces with Rainmeter or CD Art Display (or anything else that follows the cad-sdk-2.1 protocol";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(CADMusicStatusInterface).Assembly,
        "SilverAudioPlayer.Windows.MusicStatusInterface.CAD.SAPCAD.svg");

    public Version? Version => typeof(CADMusicStatusInterface).Assembly.GetName().Version;

    public string Licenses => "";

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.CAD"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri("https://web.archive.org/web/20120721202025/http://www.cdartdisplay.com/download/cad-sdk-2.1.zip"),
            URLType.LibraryDocumentation)
    };



   


    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void TrackChangedNotification(Song? newtrack)
    {
        if (!_IsStarted) return;

        var ThWnd = FindWindow(null, arglpWindowName);
        logger?.Debug("TrackChangedNotification, window found {ThWnd}", ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 123);
        logger?.Debug("TrackChangedNotification, return of 123 {e}", e);
    }

    public void PlayerStateChanged(PlaybackState newstate)
    {
        if (!_IsStarted) return;

        var ThWnd = FindWindow(null, arglpWindowName);
        logger?.Debug("PlayerStateChanged, window found {ThWnd}", ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 126);
        logger?.Debug("PlayerStateChanged, return of 126 {e}", e);
    }

    [DllImport("KERNEL32", EntryPoint = "RtlMoveMemory")]
    private static extern void CopyMemory(object hpvDest, object hpvSource, long cbCopy);

    [DllImport("user32", EntryPoint = "FindWindowA")]
    private static extern long FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32", EntryPoint = "SendMessageA")]
    private static extern long SendMessage(long hwnd, long wMsg, long wParam, CopyDataStruct lParam);

    [DllImport("user32", EntryPoint = "SendMessageA")]
    private static extern long SendMessage(long hwnd, long wMsg, long wParam, long lParam);

    [DllImport("user32", EntryPoint = "CallWindowProcA")]
    private static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, long msg, IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32", EntryPoint = "PostMessageA")]
    private static extern long PostMessage(long WndID, long wMsg, long wParam, long lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
    private static extern unsafe void MoveMemory(void* dest, void* src, int size);

    public void cadRegister()
    {
        long ThWnd;
        string strTemp;
        ThWnd = FindWindow(null, "CD Art Display 1.x Class");
        logger?.Information("Registering for CAD, found window: {ThWnd}", ThWnd);
        strTemp = "1\t\tSilverAudioPlayerIPC\t" + AppContext.BaseDirectory + Assembly.GetEntryAssembly().GetName().Name + ".exe\t";
        logger?.Debug("Registering for CAD, will send: {strTemp}", strTemp);
        CopyDataStruct copyData = new()
        {
            lpData = Marshal.StringToHGlobalUni(strTemp)
        };
        if (copyData.lpData != IntPtr.Zero)
        {
            copyData.dwData = new IntPtr(700);
            copyData.cbData = Encoding.Unicode.GetByteCount(strTemp);
            var copyDataBuff = Marshal.AllocHGlobal(Marshal.SizeOf(copyData));
            if (copyDataBuff != IntPtr.Zero)
            {
                Marshal.StructureToPtr(copyData, copyDataBuff, false);
                var e = SendMessage(ThWnd, WM_COPYDATA, 700, copyData);
                logger?.Information("Registering for CAD, got response: {e}", e);
                Marshal.FreeHGlobal(copyDataBuff);
            }

            Marshal.FreeHGlobal(copyData.lpData);
        }
    }

    private static IntPtr SetWindowProc(IntPtr hWnd, WndProcDelegate newWndProc)
    {
        var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
        return IntPtr.Size == 4
            ? SetWindowLongPtr32(hWnd, GWL_WNDPROC, newWndProcPtr)
            : SetWindowLongPtr64(hWnd, GWL_WNDPROC, newWndProcPtr);
    }

    public void HookMsg(IntPtr hwnd)
    {
        WndProcDelegate handler = CallbackMsgs;
        Handles.Add(GCHandle.Alloc(handler));

        PrevProc = SetWindowProc(hwnd, handler);
        Handles.Add(GCHandle.Alloc(PrevProc));
    }

    private string ReplaceNullWithEmptyAndTabsWithSpace(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace('\t', ' ');
    }

    public void cadSendSongInfo()
    {
        CopyDataStruct cdCopyData;
        string strTemp;

        var song = Env.GetCurrentTrack != null ? Env.GetCurrentTrack() : null;
        if (song == null) return;
        const string vbTab = "\t";
        var ThWnd = FindWindow(null, arglpWindowName);
        logger?.Information("Sending CAD the song info, CAD's window should be {ThWnd}", ThWnd);

        if (song.Metadata != null)
        {
            var art = song.URI;
            if (song.Metadata.Pictures?.Any() == true)
            {
                IPicture SelectBest(IReadOnlyList<IPicture> pictures)
                {
                    return pictures.First(x => x != null);
                }

                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Art")))
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Art"));
                var rep = Env.GetBestRepresentation(song.Metadata.Pictures);

                if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Art", rep.Hash + ".jpg")))
                {
                    using var bestart = rep.
                        Data?.GetStream();
                    using var fileStream =
                        new FileStream(Path.Combine(AppContext.BaseDirectory, "Art", rep.Hash + ".jpg"),
                            FileMode.CreateNew, FileAccess.Write);
                    bestart.CopyTo(fileStream);
                }
                  
                art = Path.Combine(AppContext.BaseDirectory, "Art", rep.Hash + ".jpg");
            }

            strTemp =
                $"{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Title)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Artist)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Album)}\t{ReplaceNullWithEmptyAndTabsWithSpace(song.Metadata.Genre)}\t{song.Metadata.Year}\t\t{song.Metadata.TrackNumber ?? 0}\t{(int)(song.Metadata.Duration / 1000 ?? 60)}\t{song.URI.Replace("\t", "\\t")}\t0\t{art.Replace("\t", "\\t")}\t\t\t\t\t\t\t";
            logger?.Debug("Sending CAD the song info, will send {strTemp}", strTemp);
        }
        else
        {
            strTemp = song.Name + vbTab + "Song Artist" + vbTab + "Song Album" + vbTab + "Song Genre" + vbTab + "Year" +
                      vbTab + "Comments" + vbTab + "1" + vbTab + "240" + vbTab + song.URI + vbTab + "5" + vbTab +
                      song.URI + vbTab + "Composer" + vbTab + "128";
            logger?.Debug("Sending CAD the song info, will send {strTemp} (DEFAULT INFO WITH SONG NAME CHANGED)",
                strTemp);
        }

        cdCopyData.dwData = new IntPtr(701);
        cdCopyData.cbData = Encoding.Unicode.GetByteCount(strTemp);
        cdCopyData.lpData = Marshal.StringToHGlobalUni(strTemp);
        var response = SendMessage(ThWnd, WM_COPYDATA, 701, cdCopyData);
        logger?.Information("Sending CAD the song info, got response: {response}", response);
    }

    public IntPtr CallbackMsgs(IntPtr Hwnd, int wMsg, IntPtr wparam, IntPtr lparam)
    {
        var wp_id = wparam.ToInt64();
        var lp_id = lparam.ToInt64();
        long CallbackMsgsRet = default;
        logger?.Debug("WP: {wp_id} LP: {lp_id}", wp_id, lp_id);

        switch (wMsg)
        {
            case WM_USER:
            {
                if (lp_id == 128)
                {
                    Env.SetRepeat( wp_id == 1 ? RepeatState.One : RepeatState.None);
                    logger?.Debug("SET Repeat: {wp_id}", wp_id);
                }

                if (lp_id == 141)
                {
                    Env.SetShuffle( wp_id == 1);
                    logger?.Debug("SET Shuffle: {wp_id}", wp_id);
                }

                if (lp_id == 114)
                {
                        Env.SetPosition( (ulong)wp_id);
                    logger?.Debug("SET Song position: {wp_id}", wp_id);

                    return new IntPtr(CallbackMsgsRet);
                }

                if (lp_id == 108)
                {
                        Env.SetVolume((byte)wp_id);
                    logger?.Debug("SET Volume: {wp_id}", wp_id);
                }

                if (lp_id == 639)
                {
                    void SetRatingI(byte val)
                    {
                        Env.SetRating(val);
                        logger?.Debug("SET RATING: {val}", val);
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
                                logger?.Debug("TOGGLE: PLAY/PAUSE");
                                Env.PlayPause();
                                break;
                            }

                            case 103:
                            {
                                logger?.Debug("TOGGLE: Stop");
                                Env.Stop();
                                break;
                            }

                            case 104:
                            {
                                logger?.Debug("TOGGLE: NEXT");
                                Env.Next();
                                if (Env.GetCurrentTrack != null && TrackChangedNotification != null)
                                    TrackChangedNotification(Env.GetCurrentTrack());
                                break;
                            }

                            case 105:
                            {
                                logger?.Debug("TOGGLE: PREVIOUS");
                                            Env.Previous();
                                if (Env.GetCurrentTrack != null && TrackChangedNotification != null)
                                    TrackChangedNotification(Env.GetCurrentTrack());
                                break;
                            }

                            case 109:
                            {
                                if (Env.GetVolume != null)
                                    CallbackMsgsRet = Env.GetVolume();
                                else
                                    CallbackMsgsRet = 70;
                                logger?.Debug("GET: VOLUME, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 110:
                            {
                                logger?.Debug("GET: SONGINF");
                                cadSendSongInfo();
                                CallbackMsgsRet = 1;
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 113:
                            {
                                if (Env.GetDuration != null)
                                    CallbackMsgsRet = (long)Env.GetDuration();
                                else
                                    CallbackMsgsRet = 0;
                                logger?.Debug("GET: SONGDUR, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 122:
                            {
                                if (Env.GetPosition != null)
                                    CallbackMsgsRet = (long)Env.GetPosition();
                                else
                                    CallbackMsgsRet = 0;
                                logger?.Debug("GET: SONGPOS, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }
                            case 125:
                            {
                                if (Env.GetState != null)
                                    CallbackMsgsRet = Env.GetState() switch
                                    {
                                        PlaybackState.Playing => 2,
                                        PlaybackState.Paused => 1,
                                        PlaybackState.Stopped => 0,
                                        _ => 0
                                    };
                                else
                                    CallbackMsgsRet = 0;
                                logger?.Debug("GET: PLAYER_STATE, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 130:
                            {
                                if (Env.GetRepeat != null)
                                    CallbackMsgsRet = Env.GetRepeat() switch
                                    {
                                        RepeatState.None => 0,
                                        RepeatState.One => 1,
                                        RepeatState.Queue => 1,
                                        _ => 0
                                    };
                                else
                                    CallbackMsgsRet = 0;
                                logger?.Debug("GET: REPEATSTATUS, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 140:
                            {
                                if (Env.GetShuffle != null)
                                    CallbackMsgsRet = Env.GetShuffle() ? 1 : 0;
                                else
                                    CallbackMsgsRet = 0;
                                logger?.Debug("GET: SHUFFLESTATUS, RETURN {CallbackMsgsRet}", CallbackMsgsRet);
                                return new IntPtr(CallbackMsgsRet);
                            }

                            case 801:
                            {
                                logger?.Debug("GET: LYRICS");
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
        return new IntPtr(CallbackMsgsRet);
    }

    public void cadSendLyrics()
    {
        CopyDataStruct cdCopyData;
        var strTemp = "";
        var ThWnd = FindWindow(null, arglpWindowName);
        var ct = Env.GetCurrentTrack != null ? Env.GetCurrentTrack() : null;
        if (ct?.Metadata != null && !string.IsNullOrEmpty(ct.Metadata.Lyrics)) strTemp = ct.Metadata.Lyrics;
        cdCopyData.dwData = new IntPtr(702);
        cdCopyData.cbData = Encoding.Unicode.GetByteCount(strTemp);
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
            strTemp = Encoding.Unicode.GetString(bytebuffer);
            strTemp = SLeft(strTemp, strTemp.IndexOf(strTemp, '\0') - 1);
            sInfo = strTemp.Split(Environment.NewLine);
        }
    }

    public static string SLeft(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        maxLength = Math.Abs(maxLength);

        return value.Length <= maxLength
                ? value
                : value[..maxLength]
            ;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            foreach (var handle in Handles) handle.Free();
            disposedValue = true;
        }
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        // 
        // CADMusicStatusInterface
        // 
        ClientSize = new Size(120, 26);
        Name = "CADMusicStatusInterface";
        ResumeLayout(false);
    }

   

    public void cadShutdown()
    {
        var ThWnd = FindWindow(null, arglpWindowName);
        logger?.Debug("cadShutdown, window found {ThWnd}", ThWnd);
        var e = SendMessage(ThWnd, WM_USER, 0, 129);
        logger?.Debug("cadShutdown, return of 129 {e}", e);
    }
    IMusicStatusInterfaceListener Env;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        Text = "SilverAudioPlayerIPC";
        _IsStarted = true;
        HookMsg(Handle);
        cadRegister();
        Show();
        Env = listener;
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        _IsStarted = false;

        cadShutdown();
        Invoke(() => { Dispose(); });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyDataStruct
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
}
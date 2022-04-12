using SilverAudioPlayer.Shared;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SilverAudioPlayer.CAD
{
    public class CADMusicStatusInterface : Form, IMusicStatusInterface
    {
        private bool disposedValue;

        [DllImport("KERNEL32", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(object hpvDest, object hpvSource, long cbCopy);

        [DllImport("user32", EntryPoint = "FindWindowA")]
        private static extern long FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32", EntryPoint = "SendMessageA")]
        private static extern long SendMessage(long hwnd, long wMsg, long wParam, COPYDATASTRUCT lParam);

        [DllImport("user32", EntryPoint = "CallWindowProcA")]
        private static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, long msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32", EntryPoint = "PostMessageA")]
        private static extern long PostMessage(long WndID, long wMsg, long wParam, long lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public void cadRegister()
        {
            long ThWnd;
            string strTemp;
            byte[] byteBuffer;
            ThWnd = FindWindow(null, "CD Art Display 1.x Class");
            Debug.WriteLine(ThWnd);

            strTemp = "1" + "\t" + "\t" + "SilverAudioPlayerIPC" + "\t" + AppContext.BaseDirectory + @"\SilverAudioPlayer.Winforms.exe" + "\t";
            COPYDATASTRUCT copyData = new COPYDATASTRUCT();
            copyData.lpData = Marshal.StringToHGlobalUni(strTemp);
            if (copyData.lpData != IntPtr.Zero)
            {
                copyData.dwData = new IntPtr(700);
                copyData.cbData = (strTemp.Length + 1) * 2;
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
            PrevProc = SetWindowProc(hwnd, handler);
        }

        private const int WM_USER = 0x400;
        public bool isTrackTwo;
        public long lPos;
        public long lVol = 100;
        public long lRepeat;
        public long lShuffle;

        public void cadSendSongInfo()
        {
            long ThWnd;
            string strTemp;
            byte[] byteBuffer;
            ThWnd = FindWindow(null, "CD Art Display 1.x Class");
            var song = GetCurrentTrack();
            if (song == null)
            {
                return;
            }
            if (song.Metadata != null)
            {
                strTemp = song.Metadata.Title + "\t" + song.Metadata.Artist + "\t" + song.Metadata.Album + "\t" + song.Metadata.Genre + "\t" + song.Metadata.Year + "\t" + string.Join(',', song.Metadata.Comments) + "\t" + song.Metadata.TrackNumber + "\t" + (ulong)(song.Metadata.Duration / 1000) ?? 60 + "\t" + song.URI + "\t" + "0" + "\t" + AppContext.BaseDirectory + @"\cover.jpg" + "\t" + "\t" + "\t" + "\t" + "\t" + "\t" + "\t";
            }
            else
            {
                strTemp = song.Name + "\t" + "Song Artist" + "\t" + "Song Album" + "\t" + "Song Genre" + "\t" + "Year" + "\t" + "Comments" + "\t" + "1" + "\t" + "240" + "\t" + @"E:\CDs\A\AaRON - Artificial Animals Riding On Neverland\05 - Blow.mp3" + "\t" + "5" + "\t" + @"E:\CDs\A\AaRON - Artificial Animals Riding On Neverland\cover.jpg" + "\t" + "Composer" + "\t" + "128";
            }
            COPYDATASTRUCT copyData = new();
            copyData.lpData = Marshal.StringToHGlobalUni(strTemp);
            if (copyData.lpData != IntPtr.Zero)
            {
                copyData.dwData = new IntPtr(700);
                copyData.cbData = (strTemp.Length + 1) * 2;
                IntPtr copyDataBuff = Marshal.AllocHGlobal(Marshal.SizeOf(copyData));
                if (copyDataBuff != IntPtr.Zero)
                {
                    Marshal.StructureToPtr(copyData, copyDataBuff, false);
                    var e = SendMessage(ThWnd, WM_COPYDATA, 701, copyData);
                    Debug.WriteLine(e);
                    Marshal.FreeHGlobal(copyDataBuff);
                }
                Marshal.FreeHGlobal(copyData.lpData);
            }
        }

        public IntPtr CallbackMsgs(IntPtr wHwnd, int wMsg, IntPtr wParam, IntPtr lParam)
        {
            long CallbackMsgsRet = default;
            switch (wMsg)
            {
                case var @case when @case == WM_USER:
                    {
                        if (lParam == new IntPtr(128))
                        {
                            SetRepeat.Invoke(this, wParam == new IntPtr(1) ? RepeatState.One : RepeatState.None);
                            Debug.WriteLine("Repeat: " + wParam);
                            return new IntPtr(CallbackMsgsRet);
                        }

                        if (lParam == new IntPtr(141))
                        {
                            Debug.WriteLine("Shuffle: " + wParam);
                            SetShuffle.Invoke(this, wParam == new IntPtr(1));
                            return new IntPtr(CallbackMsgsRet);
                        }

                        if (lParam == new IntPtr(108))
                        {
                            Debug.WriteLine("Volume " + wParam);
                            SetVolume.Invoke(this, (byte)wParam);
                            return new IntPtr(CallbackMsgsRet);
                        }

                        if (lParam == new IntPtr(639))
                        {
                            void SetRatingI(byte val)
                            {
                                Debug.WriteLine("Rating set in CAD: " + val);
                                SetRating.Invoke(this, val);
                            }
                            if (wParam == new IntPtr(0))
                            {
                                SetRatingI(0);
                                break;
                            }
                            else if (wParam == new IntPtr(1) || wParam == new IntPtr(2))
                            {
                                SetRatingI(1);
                                break;
                            }
                            else if (wParam == new IntPtr(3) || wParam == new IntPtr(4))
                            {
                                SetRatingI(2);
                                break;
                            }
                            else if (wParam == new IntPtr(5) || wParam == new IntPtr(6))
                            {
                                SetRatingI(3);
                                break;
                            }
                            else if (wParam == new IntPtr(7) || wParam == new IntPtr(8))
                            {
                                SetRatingI(4);
                                break;
                            }
                            else if (wParam == new IntPtr(9) || wParam == new IntPtr(10))
                            {
                                SetRatingI(5);
                                break;
                            }
                            return new IntPtr(CallbackMsgsRet);
                        }
                        if (wParam == new IntPtr(0))
                        {
                            if (lParam == new IntPtr(101))
                            {
                                Debug.WriteLine("Play/Pause");
                                PlayPause.Invoke(this, EventArgs.Empty);
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(103))
                            {
                                Debug.WriteLine("Stop");
                                Stop.Invoke(this, EventArgs.Empty);
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(104))
                            {
                                Debug.WriteLine("next");
                                Next.Invoke(this, EventArgs.Empty);

                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(105))
                            {
                                Debug.WriteLine("prev");
                                Previous.Invoke(this, EventArgs.Empty);

                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(109))
                            {
                                Debug.WriteLine("returning volume?");

                                CallbackMsgsRet = GetVolume();
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(110))
                            {
                                Debug.WriteLine("returning song?");
                                cadSendSongInfo();
                                CallbackMsgsRet = 1;
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(113))
                            {
                                Debug.WriteLine("returning song duration?");
                                CallbackMsgsRet = (long)GetDuration();
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(122))
                            {
                                Debug.WriteLine("returning song position?");
                                //in seconds
                                CallbackMsgsRet = (long)GetPosition();
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(125))
                            {
                                Debug.WriteLine("returning PLAYER_STATE (code 125)");
                                CallbackMsgsRet = GetState() switch
                                {
                                    PlaybackState.Playing => 1,
                                    PlaybackState.Paused => 2,
                                    PlaybackState.Stopped => 0,
                                    _ => 0
                                };

                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(130))
                            {
                                Debug.WriteLine("returning repeat status? (code 130)");
                                CallbackMsgsRet = GetRepeat() switch
                                {
                                    RepeatState.None => 0,
                                    RepeatState.One => 1,
                                    RepeatState.Queue => 1,
                                    _ => 0
                                };
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(140))
                            {
                                Debug.WriteLine("returning shuffle status? (code 140)");
                                CallbackMsgsRet = GetShuffle() ? 1 : 0;
                                return new IntPtr(CallbackMsgsRet);
                            }
                            else if (lParam == new IntPtr(801))
                            {
                                Debug.WriteLine("returning lytics status? (code 801)");
                                //cadSendLyrics();
                                CallbackMsgsRet = 0L;
                                return new IntPtr(CallbackMsgsRet);
                            }
                        }

                        break;
                    }

                case var case1 when case1 == WM_COPYDATA:
                    {
                        Debug.WriteLine("Got into wmcopydata??A??");
                        //InterProcessComms(lp_id);
                        break;
                    }
            }

            CallbackMsgsRet = CallWindowProc(PrevProc, wHwnd, wMsg, wParam, lParam);
            return new IntPtr(CallbackMsgsRet);
        }

        private const int WM_COPYDATA = 0x4A;

        public event EventHandler Play;

        public event EventHandler Pause;

        public event EventHandler PlayPause;

        public event EventHandler Stop;

        public event EventHandler Next;

        public event EventHandler Previous;

        public event EventHandler<byte> SetVolume;

        public event EventHandler<byte> VolumeChangedNotification;

        public event EventHandler<ulong> SetPosition;

        public event EventHandler<IMusicStatusInterface> TrackChangedNotification;

        public event EventHandler<IMusicStatusInterface> ShowWindow;

        public event EventHandler<IMusicStatusInterface> StateChangedNotification;

        public event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

        public event EventHandler<RepeatState> SetRepeat;

        public event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

        public event EventHandler<IMusicStatusInterface> Close;

        public event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

        public event EventHandler<bool> SetShuffle;

        public event EventHandler<IMusicStatusInterface> RatingChangedNotification;

        public event EventHandler<byte> SetRating;

        public event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

        public event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

        public event EventHandler<IMusicStatusInterface> NewLyricsNotification;

        public event EventHandler<IMusicStatusInterface> NewCoverNotification;

        public event Func<byte> GetVolume;

        public event Func<Song> GetCurrentTrack;

        public event Func<ulong> GetDuration;

        public event Func<ulong> GetPosition;

        public event Func<PlaybackState> GetState;

        public event Func<RepeatState> GetRepeat;

        public event Func<bool> GetShuffle;

        public event Func<string> GetLyrics;

        public void StartIPC()
        {
            Text = "SilverAudioPlayerIPC";
            Show();
            ///window.Visible = false;
            //window.WindowState = FormWindowState.Minimized;
            //window.ShowInTaskbar = false;
            //window.Hide();
            HookMsg(Handle);
            cadRegister();
        }

        public void StopIPC()
        {
            Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
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
    }
}
using SilverAudioPlayer.Shared;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SilverAudioPlayer.CAD
{
    public class CADMusicStatusInterface : Form, IMusicStatusInterface
    {
        public CADMusicStatusInterface()
        {
            InitializeComponent();
        }

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

            strTemp = "1" + "\t" + "\t" + "SilverAudioPlayerIPC" + "\t" + AppContext.BaseDirectory + @"SilverAudioPlayer.Winforms.exe" + "\t";
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
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
        private Label label7;
        private Label label11;
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
                strTemp = $"{song.Metadata.Title}\t{song.Metadata.Artist}\t{song.Metadata.Album}\t{song.Metadata.Genre}\t{song.Metadata.Year}\t\t{(song.Metadata.TrackNumber ?? 0)}\t{0}\t{song.Metadata.Duration / 1000 ?? 60}\t{song.URI}\t0\t \t\t\t\t\t\t\t";
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

        private List<GCHandle> Handles = new();

        private const int WM_COPYDATA = 0x4A;

        public event EventHandler Play;

        public event EventHandler Pause;

        public event EventHandler PlayPause;

        public event EventHandler Stop;

        public event EventHandler Next;

        public event EventHandler Previous;

        public event EventHandler<byte> SetVolume;

        public event EventHandler<ulong> SetPosition;

        public event EventHandler<RepeatState> SetRepeat;

        public event EventHandler<bool> SetShuffle;

        public event EventHandler<byte> SetRating;

        public event Func<byte> GetVolume;

        public event Func<Song> GetCurrentTrack;

        public event Func<ulong> GetDuration;

        public event Func<ulong> GetPosition;

        public event Func<PlaybackState> GetState;

        public event Func<RepeatState> GetRepeat;

        public event Func<bool> GetShuffle;

        public event Func<string> GetLyrics;

        public event EventHandler<IMusicStatusInterface> StateChangedNotification;

        public event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

        public event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

        public event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

        public event EventHandler<IMusicStatusInterface> RatingChangedNotification;

        public event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

        public event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

        public event EventHandler<IMusicStatusInterface> NewLyricsNotification;

        public event EventHandler<IMusicStatusInterface> NewCoverNotification;

        public void StartIPC()
        {
            Text = "SilverAudioPlayerIPC";
            //Show();
            ///window.Visible = false;
            //window.WindowState = FormWindowState.Minimized;
            //window.ShowInTaskbar = false;
            //window.Hide();
            HookMsg(Handle);
            cadRegister();
            //Visible = false;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
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
                foreach (var handle in Handles)
                {
                    handle.Free();
                }
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

        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(22, 24);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(222, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Hey there, your not supposed to be here.";
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(22, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(222, 15);
            this.label2.TabIndex = 1;
            this.label2.Text = "Hey there, your not supposed to be here.";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(22, 73);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(222, 15);
            this.label3.TabIndex = 2;
            this.label3.Text = "Hey there, your not supposed to be here.";
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(22, 98);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(222, 15);
            this.label4.TabIndex = 3;
            this.label4.Text = "Hey there, your not supposed to be here.";
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 126);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(222, 15);
            this.label5.TabIndex = 4;
            this.label5.Text = "Hey there, your not supposed to be here.";
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(22, 151);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(222, 15);
            this.label6.TabIndex = 5;
            this.label6.Text = "Hey there, your not supposed to be here.";
            //
            // label7
            //
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(22, 175);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(222, 15);
            this.label7.TabIndex = 6;
            this.label7.Text = "Hey there, your not supposed to be here.";
            //
            // label8
            //
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(22, 200);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(222, 15);
            this.label8.TabIndex = 7;
            this.label8.Text = "Hey there, your not supposed to be here.";
            //
            // label9
            //
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(22, 224);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(246, 15);
            this.label9.TabIndex = 8;
            this.label9.Text = "BTW this is how we windows applications talk\r\n";
            //
            // label10
            //
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(22, 248);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(164, 15);
            this.label10.TabIndex = 9;
            this.label10.Text = "by spawning hidden windows\r\n";
            //
            // label11
            //
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(22, 263);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(97, 15);
            this.label11.TabIndex = 10;
            this.label11.Text = "Pretty cool right?";
            //
            // CADMusicStatusInterface
            //
            this.ClientSize = new System.Drawing.Size(284, 287);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "CADMusicStatusInterface";
            this.Load += new System.EventHandler(this.CADMusicStatusInterface_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void CADMusicStatusInterface_Load(object sender, EventArgs e)
        {
            //Visible = false;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
        }

        public void TrackChangedNotification(Song newtrack)
        {
            long ThWnd;
            ThWnd = FindWindow(null, "CD Art Display 1.x Class");
            Debug.WriteLine(ThWnd);
            var e = PostMessage(ThWnd, WM_USER, 0, 123);
            Debug.WriteLine(e);
        }

        public void PlayerStateChanged(PlaybackState newstate)
        {
            long ThWnd;
            ThWnd = FindWindow(null, "CD Art Display 1.x Class");
            Debug.WriteLine(ThWnd);
            var e = PostMessage(ThWnd, WM_USER, 0, 125);
            Debug.WriteLine(e);
        }

        private Label label8;
        private Label label9;
        private Label label10;
    }
}
using SilverAudioPlayer.Shared;
using System.Composition;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.Windows.PlatformHelper.Win
{
    [Export(typeof(IWakeLockProvider))]
    public class Win32WakeLock : IWakeLockProvider
    {
        public string Name => "Win32 WakeLock provider";

        public string Description => "Uses the SetThreadExecutionState API";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(Win32WakeLock).Assembly, "SilverAudioPlayer.Windows.PlatformHelper.Win.Win32WakeLock.png");
        public Version? Version => typeof(Win32WakeLock).Assembly.GetName().Version;

        public string Licenses => "GPL3.0";

        public List<Tuple<Uri, URLType>>? Links => new() { new(new("https://github.com/thesilvercraft/SilverAudioPlayer/SilverAudioPlayer.Windows.PlatformHelper.Win10"),URLType.Code), new(new("https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate"),URLType.LibraryDocumentation)};

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
        [Flags]
        public enum ExecutionState : uint
        {
            AwayModeRequired = 0x00000040,
            Continuous = 0x80000000,
            DisplayRequired = 0x00000002,
            SystemRequired = 0x00000001
        }

        public void WakeLock()
        {
            SetThreadExecutionState(ExecutionState.Continuous | ExecutionState.SystemRequired | ExecutionState.AwayModeRequired);
        }

        public void UnWakeLock()
        {
            SetThreadExecutionState(ExecutionState.Continuous);
        }
    }
}
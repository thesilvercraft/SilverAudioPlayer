namespace SilverAudioPlayer.Windows.MusicStatusInterface.SMTC;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class NativeWindowHelper
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private const uint GW_OWNER = 4;

    public static IntPtr GetMainWindowHandle()
    {
        uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
        IntPtr foundHwnd = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint processId);

            // Match process ID, ensure window is visible and is a top-level window (no owner)
            if (processId == currentProcessId && IsWindowVisible(hWnd) && GetWindow(hWnd, GW_OWNER) == IntPtr.Zero)
            {
                foundHwnd = hWnd;
                return false; // Stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        return foundHwnd;
    }
}
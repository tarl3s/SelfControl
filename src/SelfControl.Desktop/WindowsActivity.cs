using System.Runtime.InteropServices;
using Microsoft.Win32;
using SelfControl.Core;

namespace SelfControl.Desktop;

/// <summary>Idle time from GetLastInputInfo (keyboard and mouse); screen lock from session switch events.</summary>
sealed class WindowsActivity : IActivity, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    volatile bool _locked;

    public WindowsActivity() => SystemEvents.SessionSwitch += OnSwitch;

    void OnSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect) _locked = true;
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect) _locked = false;
    }

    public bool ScreenLocked => _locked;

    public TimeSpan IdleFor
    {
        get
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        }
    }

    public void Dispose() => SystemEvents.SessionSwitch -= OnSwitch;
}

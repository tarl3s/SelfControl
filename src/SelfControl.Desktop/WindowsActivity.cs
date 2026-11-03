using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SelfControl.Core;

namespace SelfControl.Desktop;

/// <summary>Idle time from GetLastInputInfo (keyboard and mouse); screen lock from session switch events; the program
/// in front from the foreground window; the site in front as reported by the browser extension.</summary>
sealed class WindowsActivity : IActivity, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
        { "brave", "chrome", "msedge", "opera", "opera_gx", "vivaldi", "firefox", "chromium", "arc" };
    static readonly TimeSpan SiteFresh = TimeSpan.FromMinutes(2);

    volatile bool _locked;
    readonly object _gate = new();
    string? _site; DateTime _siteAt;
    readonly Dictionary<uint, (string exe, string name)> _names = new();

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

    /// <summary>Called by the extension bridge (background thread) with the active tab's host, or "".</summary>
    public void ReportSite(string host)
    {
        lock (_gate) { _site = string.IsNullOrWhiteSpace(host) ? null : host.StartsWith("www.") ? host[4..] : host; _siteAt = DateTime.UtcNow; }
    }

    (string exe, string name)? Foreground()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero) return null;
        GetWindowThreadProcessId(h, out var pid);
        if (pid == 0) return null;
        lock (_gate)
        {
            if (_names.TryGetValue(pid, out var known)) return known;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                string exe = p.ProcessName, name = exe;
                try
                {
                    var file = p.MainModule?.FileName;
                    if (file != null)
                    {
                        var v = FileVersionInfo.GetVersionInfo(file);
                        name = !string.IsNullOrWhiteSpace(v.FileDescription) ? v.FileDescription!.Trim()
                             : !string.IsNullOrWhiteSpace(v.ProductName) ? v.ProductName!.Trim() : exe;
                    }
                }
                catch (Exception) { /* protected process: keep the exe name */ }
                if (_names.Count > 500) _names.Clear();
                return _names[pid] = (exe, name);
            }
            catch (Exception) { return null; }
        }
    }

    public string? ForegroundApp => Foreground()?.name;

    public string? ActiveSite
    {
        get
        {
            if (Foreground() is not { } f || !Browsers.Contains(f.exe)) return null;
            lock (_gate) return DateTime.UtcNow - _siteAt < SiteFresh ? _site : null;
        }
    }

    public void Dispose() => SystemEvents.SessionSwitch -= OnSwitch;
}

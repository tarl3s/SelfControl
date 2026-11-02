using System.Diagnostics;
using SelfControl.Core;

namespace SelfControl.Desktop;

/// <summary>Polls running processes once a second. NOTE: AppSeen/AppClosed are raised on a background
/// timer thread; subscribers must marshal to their own thread / lock shared state.</summary>
public sealed class WindowsBlocker : IBlocker, IDisposable
{
    public event Action<string>? AppSeen;
    public event Action<string>? AppClosed;

    readonly object _gate = new();
    HashSet<string> _watched = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    readonly Timer _timer;
    int _busy;

    public WindowsBlocker()
    {
        _timer = new Timer(_ => Tick(), null, 1000, 1000);
    }

    public void SetWatched(IEnumerable<string> exes)
    {
        lock (_gate)
        {
            _watched = new HashSet<string>(exes.Select(BlockRules.NormalizeApp), StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyCollection<string> Running { get { lock (_gate) return _running.ToArray(); } }

    public void Kill(string exe)
    {
        var name = BlockRules.NormalizeApp(exe);
        if (name.EndsWith(".exe")) name = name[..^4];
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)) p.Kill();
            }
            catch { /* access denied / already exited */ }
            finally { p.Dispose(); }
        }
    }

    void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try { names.Add(p.ProcessName + ".exe"); } catch { }
                finally { p.Dispose(); }
            }
            var seen = new List<string>();
            var closed = new List<string>();
            lock (_gate)
            {
                foreach (var w in _watched)
                    if (names.Contains(w) && _running.Add(w)) seen.Add(w);
                foreach (var r in _running.ToArray())
                    if (!_watched.Contains(r) || !names.Contains(r)) { _running.Remove(r); closed.Add(r); }
            }
            foreach (var s in seen) AppSeen?.Invoke(s);
            foreach (var c in closed) AppClosed?.Invoke(c);
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    public void Dispose() => _timer.Dispose();
}

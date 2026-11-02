using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.App;

/// <summary>Owns the open warning windows: one per blocked exe, closed when dismissed or when the lock lifts.</summary>
public sealed class WarningManager : IDisposable
{
    readonly AppState _state;
    readonly Dictionary<string, BlockWarningWindow> _open = new(StringComparer.OrdinalIgnoreCase);

    public WarningManager(AppState state)
    {
        _state = state;
        state.AppBlocked += OnBlocked;
        state.WarningDismissed += Dismiss;
        state.LockLifted += CloseAll;
    }

    public int OpenCount => _open.Count;
    public BlockWarningWindow? Get(string exe) => _open.GetValueOrDefault(exe);

    void OnBlocked(string exe, BlockPhase phase)
    {
        if (_open.ContainsKey(exe)) return; // already warning about it
        var w = new BlockWarningWindow(_state, exe, phase);
        _open[exe] = w;
        w.Show();
    }

    void Dismiss(string exe)
    {
        if (_open.Remove(exe, out var w)) w.Dismiss();
    }

    void CloseAll()
    {
        var all = _open.Values.ToList();
        _open.Clear();
        foreach (var w in all) w.Dismiss();
    }

    public void Dispose()
    {
        _state.AppBlocked -= OnBlocked;
        _state.WarningDismissed -= Dismiss;
        _state.LockLifted -= CloseAll;
        CloseAll();
    }
}

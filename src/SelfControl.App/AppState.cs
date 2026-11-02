using Avalonia.Threading;
using SelfControl.Core;

namespace SelfControl.App;

/// <summary>
/// Single owner of <see cref="AppData"/> and the <see cref="BreaktimeTracker"/>.
/// THREADING: all state lives on the Avalonia UI thread. Callbacks from background threads (blocker events, the
/// extension bridge) must go through <see cref="OnUi{T}"/> / <see cref="OnUiAsync"/>, which marshal to
/// <see cref="Dispatcher.UIThread"/>; nothing else locks.
/// </summary>
public sealed class AppState : IDisposable
{
    readonly DataStore _store;
    readonly IBlocker _blocker;
    readonly INotifier _notifier;
    readonly Func<DateTime> _now;
    readonly DispatcherTimer _saveTimer;
    readonly DispatcherTimer _tickTimer;
    DateTime _lastCheck;
    DateOnly _today;
    bool _wasLocked;
    readonly HashSet<string> _retryClose = new(StringComparer.OrdinalIgnoreCase);
    const int UndoLimit = 100;
    readonly List<string> _undo = new();
    string _snapshot;

    public AppData Data { get; private set; }
    public BreaktimeTracker Tracker { get; }
    public IAutostart Autostart { get; }
    /// <summary>Installed programs for the block-list picker (null where the platform has none, e.g. tests).</summary>
    public IAppCatalog? Apps { get; init; }
    public bool LoadFailed => _store.LoadFailed;
    /// <summary>Where Início copies chosen pictures (same base folder as data.json).</summary>
    public string ImagesFolder => Path.Combine(_store.Folder, "images");
    public DateTime Now => _now();

    /// <summary>Raised on the UI thread when a watched app is running while locked and needs a warning (Task 8 shows it).</summary>
    public event Action<string, BlockPhase>? AppBlocked;
    /// <summary>Raised on the UI thread when views should refresh (data changed, or the day changed at midnight).</summary>
    public event Action? ViewsChanged;
    /// <summary>Raised when an app's warning must go away (it was closed, killed, or got a break).</summary>
    public event Action<string>? WarningDismissed;
    /// <summary>Raised when the last task of today is checked: every open warning closes.</summary>
    public event Action? LockLifted;
    /// <summary>Raised on the UI thread after each hour of use (when break reminders are on): suggest exercises.</summary>
    public event Action? BreakDue;
    readonly BreakClock _breaks;
    readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _breaking = new(StringComparer.OrdinalIgnoreCase);
    readonly DispatcherTimer _breakTimer;

    public AppState(DataStore store, IBlocker blocker, INotifier notifier, IAutostart autostart,
        bool firstRun = false, Func<DateTime>? now = null)
    {
        _store = store; _blocker = blocker; _notifier = notifier; Autostart = autostart;
        _now = now ?? (() => DateTime.Now);
        Data = store.Load();
        Tracker = new BreaktimeTracker(_now);
        _lastCheck = _now();
        _today = DateOnly.FromDateTime(_lastCheck);
        if (firstRun && Data.Home.Count == 0)
        {
            Data.Home.Add(new HomeBlock { Kind = HomeBlockKind.Text, Text = "Bem-vindo" });
            Data.Home.Add(new HomeBlock { Kind = HomeBlockKind.TodayRoutine });
            Data.Home.Add(new HomeBlock { Kind = HomeBlockKind.TodayTasks });
        }
        if (firstRun) { try { autostart.Enabled = true; } catch { /* registry unavailable: not fatal */ } }
        EventScheduler.RemovePast(Data.Events, _lastCheck);
        _breaks = new BreakClock(_lastCheck);
        _snapshot = DataStore.Snapshot(Data);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => SaveNow();
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _tickTimer.Tick += (_, _) => Tick();

        _breakTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _breakTimer.Tick += (_, _) => CheckBreaks();
        _blocker.AppSeen += exe => OnUiAsync(() => OnAppSeen(exe));
        _blocker.AppClosed += exe => OnUiAsync(() => AppGone(exe));
        RefreshWatched();
        _wasLocked = IsLocked;
        _tickTimer.Start();
    }

    public static T OnUi<T>(Func<T> f) =>
        Dispatcher.UIThread.CheckAccess() ? f() : Dispatcher.UIThread.Invoke(f);
    public static void OnUiAsync(Action a) => Dispatcher.UIThread.InvokeAsync(a);

    /// <summary>Debounced save (500 ms). Call after any change to <see cref="Data"/>.</summary>
    public void Save()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        try { _store.Save(Data); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Views call this after editing data: saves, re-evaluates the lock and the watched apps, refreshes views.</summary>
    public void DataChanged()
    {
        var snap = DataStore.Snapshot(Data);
        if (snap != _snapshot)
        {
            _undo.Add(_snapshot);
            if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
            _snapshot = snap;
        }
        Refresh();
    }

    public bool CanUndo => _undo.Count > 0;

    /// <summary>Goes back to the data as it was before the last change (Ctrl+Z). False when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Data = DataStore.FromSnapshot(_snapshot);
        Refresh();
        return true;
    }

    void Refresh()
    {
        Save();
        RefreshWatched();
        RefreshLock();
        ViewsChanged?.Invoke();
    }

    public void ReplaceData(AppData data)
    {
        Data = data;
        DataChanged();
    }

    public void Export(string path) => _store.Export(Data, path);

    /// <summary>Replaces all data with the file's. Returns an error message for the user, or null on success.</summary>
    public string? Import(string path)
    {
        try
        {
            var data = _store.Import(path);
            _store.AcceptReplacement();
            Tracker.Clear(); _breaking.Clear();
            ReplaceData(data); SaveNow();
            return null;
        }
        catch (InvalidDataException) { return "Arquivo inválido"; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "Não foi possível ler o arquivo"; }
    }

    public bool IsLocked => TaskRules.IsLocked(Data, DateOnly.FromDateTime(_now()));

    /// <summary>When nothing is locked any more, forget breaks/warnings so a later relock starts fresh.</summary>
    public void RefreshLock()
    {
        var locked = IsLocked;
        var was = _wasLocked;
        _wasLocked = locked;
        if (locked)
        {
            // Lock came back (midnight, task added/unchecked, break ended): apps already running need their warning.
            if (!was) foreach (var exe in _blocker.Running.ToList()) OnAppSeen(exe);
            return;
        }
        Tracker.Clear();
        _breaking.Clear(); _breakTimer.Stop(); _retryClose.Clear();
        if (was) LockLifted?.Invoke();
    }

    void RefreshWatched() => _blocker.SetWatched(Data.BlockList.Apps.Where(BlockRules.CanBlockApp));

    void OnAppSeen(string exe)
    {
        _running.Add(exe);
        if (IsLocked) _wasLocked = true; // a warning may be open even if nobody re-evaluated the lock yet
        var phase = Tracker.OnSeen(exe, IsLocked);
        if (phase is BlockPhase.Warning or BlockPhase.Expired) AppBlocked?.Invoke(exe, phase);
    }

    public void Tick()
    {
        var now = _now();
        try
        {
            foreach (var ev in EventScheduler.Due(Data.Events, _lastCheck, now))
                _notifier.Show("SelfControl", ev.Message);
        }
        catch (Exception) { /* a toast failure must not crash the app */ }
        _lastCheck = now;
        if (EventScheduler.RemovePast(Data.Events, now) > 0) DataChanged();
        if (_breaks.Tick(now) && Data.BreakReminders) BreakDue?.Invoke();
        RefreshWatched();
        var today = DateOnly.FromDateTime(now);
        var newDay = today != _today;
        _today = today;
        RefreshLock();
        if (IsLocked && _retryClose.Count > 0)
        {
            var still = _blocker.Running;
            foreach (var exe in _retryClose.ToList())
            {
                _retryClose.Remove(exe);
                if (still.Contains(exe, StringComparer.OrdinalIgnoreCase)) OnAppSeen(exe); // Kill failed: warn again
            }
        }
        if (newDay) ViewsChanged?.Invoke();
    }

    void AppGone(string exe)
    {
        _running.Remove(exe); _breaking.Remove(exe); _retryClose.Remove(exe);
        Tracker.OnClosed(exe);
        WarningDismissed?.Invoke(exe);
    }

    /// <summary>"Breaktime": lets a blocked app run for a while; its warning closes and a 1 s timer watches for the end.</summary>
    public void StartAppBreak(string exe, int minutes)
    {
        Tracker.StartBreak(exe, minutes);
        if (Tracker.Remaining(exe) != null) { _breaking.Add(exe); _breakTimer.Start(); }
        WarningDismissed?.Invoke(exe);
    }

    /// <summary>"Fechar": kills the blocked app and forgets it, so reopening warns again.</summary>
    public void CloseApp(string exe)
    {
        try { _blocker.Kill(exe); } catch (Exception) { }
        AppGone(exe);
        _retryClose.Add(exe); // if it survived the kill, the next tick warns again
    }

    /// <summary>Re-evaluates apps on a break; raises AppBlocked(Expired) once for each whose break ended while it runs.</summary>
    public void CheckBreaks()
    {
        foreach (var exe in _breaking.ToList())
        {
            if (!_running.Contains(exe)) { _breaking.Remove(exe); continue; }
            if (Tracker.OnSeen(exe, IsLocked) != BlockPhase.Expired) continue;
            _breaking.Remove(exe);
            AppBlocked?.Invoke(exe, BlockPhase.Expired);
        }
        if (_breaking.Count == 0) _breakTimer.Stop();
    }

    // ---- Extension bridge delegates (called from the bridge's background thread) ----

    string SiteKey(string host) =>
        Data.BlockList.Sites.FirstOrDefault(s => BlockRules.MatchesSite(host, s)) ?? host;

    public BridgeInfo Check(string host) => OnUi(() =>
    {
        var site = Data.BlockList.Sites.FirstOrDefault(s => BlockRules.MatchesSite(host, s));
        var today = DateOnly.FromDateTime(_now());
        if (site == null || !TaskRules.IsLocked(Data, today))
        {
            RefreshLock();
            return new BridgeInfo(false, "Free", Array.Empty<string>(), 0);
        }
        var phase = Tracker.OnSeen(site, true);
        var pending = TaskRules.Today(Data, today).Where(t => !TaskRules.IsDone(t, today)).Select(t => t.Title).ToList();
        var rem = phase == BlockPhase.Break ? (int)Math.Ceiling(Tracker.Remaining(site)?.TotalSeconds ?? 0) : 0;
        return new BridgeInfo(true, phase.ToString(), pending, rem);
    });

    public void StartBreak(string site, int minutes) => OnUi(() => { Tracker.StartBreak(SiteKey(site), minutes); return 0; });
    public void SiteClosed(string site) => OnUi(() => { Tracker.OnClosed(SiteKey(site)); return 0; });

    public void Dispose()
    {
        _tickTimer.Stop(); _breakTimer.Stop();
        SaveNow();
    }
}

/// <summary>What the extension bridge reports; Desktop maps it to its BridgeStatus (App cannot reference Desktop).</summary>
public record BridgeInfo(bool Blocked, string Phase, IReadOnlyList<string> Tasks, int Remaining);

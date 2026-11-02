using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class AppStateTests
{
    sealed class FakeBlocker : IBlocker
    {
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
    }
    sealed class FakeNotifier : INotifier { public List<string> Shown = new(); public void Show(string t, string m) => Shown.Add(m); }
    sealed class FakeAutostart : IAutostart { public bool Enabled { get; set; } }

    [AvaloniaFact]
    public void Check_BlocksSiteWhileLocked_AndResetsWhenUnlocked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-ui-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var now = new DateTime(2026, 11, 3, 10, 0, 0);
            var auto = new FakeAutostart();
            var s = new AppState(new DataStore(dir), new FakeBlocker(), new FakeNotifier(), auto, firstRun: true, now: () => now);
            Assert.True(auto.Enabled);
            var task = new TaskItem { Title = "Estudar", TempDate = DateOnly.FromDateTime(now) };
            s.Data.Tasks.Add(task);
            s.Data.BlockList.Sites.Add("youtube.com");
            var st = s.Check("www.youtube.com");
            Assert.True(st.Blocked); Assert.Equal("Warning", st.Phase); Assert.Equal(new[] { "Estudar" }, st.Tasks);
            s.StartBreak("youtube.com", 5);
            var br = s.Check("youtube.com");
            Assert.Equal("Break", br.Phase); Assert.Equal(300, br.Remaining);
            TaskRules.SetDone(task, DateOnly.FromDateTime(now), true);
            Assert.False(s.Check("youtube.com").Blocked);
            task.DoneOn.Clear();
            Assert.Equal("Warning", s.Check("youtube.com").Phase);
            s.Dispose();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

public class LockComesBackTests
{
    sealed class RunningBlocker : IBlocker
    {
        public HashSet<string> Set = new(StringComparer.OrdinalIgnoreCase);
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
        public IReadOnlyCollection<string> Running => Set;
    }
    sealed class Nope : INotifier { public void Show(string t, string m) { } }
    sealed class Auto : IAutostart { public bool Enabled { get; set; } }

    static (AppState s, RunningBlocker b, string dir) Make(Func<DateTime> now)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-ui-" + Guid.NewGuid().ToString("N")[..8]);
        var b = new RunningBlocker();
        var s = new AppState(new DataStore(dir), b, new Nope(), new Auto(), now: now);
        s.Data.BlockList.Apps.Add("game.exe");
        return (s, b, dir);
    }

    [AvaloniaFact]
    public void Midnight_WithRunningApp_WarnsOnce_AndLockLiftedNotRepeated()
    {
        var now = new DateTime(2026, 11, 3, 23, 59, 0);
        var (s, b, dir) = Make(() => now);
        try
        {
            var warned = new List<(string, BlockPhase)>(); int lifted = 0;
            s.AppBlocked += (e, p) => warned.Add((e, p)); s.LockLifted += () => lifted++;
            b.Set.Add("game.exe");
            s.Data.Tasks.Add(new TaskItem { Title = "Dia 4", TempDate = new DateOnly(2026, 11, 4) });
            s.Tick();
            Assert.Empty(warned);
            now = new DateTime(2026, 11, 4, 0, 1, 0);
            s.Tick(); s.Tick();
            Assert.Equal(new[] { ("game.exe", BlockPhase.Warning) }, warned);
            var d4 = DateOnly.FromDateTime(now);
            TaskRules.SetDone(s.Data.Tasks[0], d4, true);
            s.Tick(); Assert.Equal(1, lifted);
            s.Tick(); s.Tick(); Assert.Equal(1, lifted);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void UncheckingDoneTask_WithRunningApp_Warns()
    {
        var now = new DateTime(2026, 11, 3, 10, 0, 0);
        var (s, b, dir) = Make(() => now);
        try
        {
            var day = DateOnly.FromDateTime(now);
            var t = new TaskItem { Title = "X", TempDate = day };
            TaskRules.SetDone(t, day, true);
            s.Data.Tasks.Add(t);
            s.DataChanged();
            b.Set.Add("game.exe");
            var warned = new List<string>();
            s.AppBlocked += (e, p) => warned.Add(e);
            t.DoneOn.Clear();
            s.DataChanged();
            Assert.Equal(new[] { "game.exe" }, warned);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }
}

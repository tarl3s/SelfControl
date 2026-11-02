using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class BreakTests
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

    static (AppState s, FakeNotifier n, Func<DateTime> clock, Action<TimeSpan> advance, string dir) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-brk-" + Guid.NewGuid().ToString("N")[..8]);
        var now = new DateTime(2026, 11, 2, 9, 0, 0);
        var n = new FakeNotifier();
        var s = new AppState(new DataStore(dir), new FakeBlocker(), n, new FakeAutostart(), now: () => now);
        return (s, n, () => now, d => now += d, dir);
    }

    [AvaloniaFact]
    public void Tick_AfterAnHour_RaisesBreakDue_UnlessTurnedOff()
    {
        var (s, _, _, advance, dir) = Make();
        try
        {
            int due = 0; s.BreakDue += () => due++;
            for (int i = 0; i < 120; i++) { advance(TimeSpan.FromSeconds(30)); s.Tick(); }
            Assert.Equal(1, due);
            s.Data.BreakReminders = false;
            for (int i = 0; i < 120; i++) { advance(TimeSpan.FromSeconds(30)); s.Tick(); }
            Assert.Equal(1, due);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void OneTimeEvent_FiresThenIsDeleted()
    {
        var (s, n, clock, advance, dir) = Make();
        try
        {
            s.Data.Events.Add(new ReminderEvent { Message = "dentista", Time = new TimeOnly(9, 1), Rule = DateRule.On(new DateOnly(2026, 11, 2)) });
            s.DataChanged();
            advance(TimeSpan.FromSeconds(90)); s.Tick();
            Assert.Equal(new[] { "dentista" }, n.Shown);
            Assert.Single(s.Data.Events);                 // still inside its 2-minute window
            advance(TimeSpan.FromMinutes(3)); s.Tick();
            Assert.Empty(s.Data.Events);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void ExerciseWindows_OpenAndClose()
    {
        BreakWindows.ShowStretches().Close();
        BreakWindows.ShowEyes().Close();
        BreakWindows.ShowPrompt();
    }

    [Fact]
    public void EyeBall_StaysOnScreen_AndNearFarChangesSize()
    {
        for (int p = 0; p < 5; p++)
            for (double t = 0; t < 10; t += 0.1)
            {
                var (x, y, sc) = BreakWindows.EyeBall(p, t);
                Assert.InRange(x, 0.05, 0.95); Assert.InRange(y, 0.05, 0.95); Assert.True(sc > 0);
            }
        Assert.NotEqual(BreakWindows.EyeBall(4, 0).Scale, BreakWindows.EyeBall(4, 1.25).Scale);
    }
}

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

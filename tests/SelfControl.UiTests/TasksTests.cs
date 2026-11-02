using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class TasksTests
{
    sealed class FakeBlocker : IBlocker
    {
        public event Action<string>? AppSeen;
        public event Action<string>? AppClosed;
        public List<string> Killed = new();
        public void Kill(string exe) => Killed.Add(exe);
        public void SetWatched(IEnumerable<string> exes) { }
        public void Seen(string exe) { AppSeen?.Invoke(exe); Dispatcher.UIThread.RunJobs(); }
        public void Closed(string exe) { AppClosed?.Invoke(exe); Dispatcher.UIThread.RunJobs(); }
    }
    sealed class FakeNotifier : INotifier { public void Show(string t, string m) { } }
    sealed class FakeAutostart : IAutostart { public bool Enabled { get; set; } }

    sealed class Rig : IDisposable
    {
        public DateTime Now = new(2026, 11, 3, 10, 0, 0);
        public string Dir = Path.Combine(Path.GetTempPath(), "sc-ui-" + Guid.NewGuid().ToString("N")[..8]);
        public FakeBlocker Blocker = new();
        public AppState State;
        public WarningManager Manager;
        public TaskItem Task;
        public Rig()
        {
            State = new AppState(new DataStore(Dir), Blocker, new FakeNotifier(), new FakeAutostart(), now: () => Now);
            Task = new TaskItem { Title = "Estudar", TempDate = DateOnly.FromDateTime(Now) };
            State.Data.Tasks.Add(Task);
            State.Data.BlockList.Apps.Add("game.exe");
            Manager = new WarningManager(State);
        }
        public void Dispose()
        {
            Manager.Dispose(); State.Dispose();
            try { Directory.Delete(Dir, true); } catch { }
        }
    }

    [AvaloniaFact]
    public void ProgramWithTwoExes_OneWarning_OneBreak_OneClose()
    {
        using var r = new Rig();
        RuleText.TryAddProgram(r.State.Data.BlockList, new InstalledApp("Steam", "Valve", new[] { "steam.exe", "steamwebhelper.exe" }, null));
        r.Blocker.Seen("steam.exe"); r.Blocker.Seen("steamwebhelper.exe");
        Assert.Equal(1, r.Manager.OpenCount);                       // one warning for the whole program
        r.Manager.Get("Steam")!.StartBreak(10);
        Assert.Equal(0, r.Manager.OpenCount);
        r.Blocker.Seen("steamwebhelper.exe");                       // a helper restarting during the break: no warning
        Assert.Equal(0, r.Manager.OpenCount);
        r.Now = r.Now.AddMinutes(11); r.State.CheckBreaks();
        Assert.Equal(1, r.Manager.OpenCount);                       // one expired warning
        r.State.CloseApp("Steam");                                  // what its single Close button does
        Assert.Equal(new[] { "steam.exe", "steamwebhelper.exe" }, r.Blocker.Killed.Distinct().OrderBy(x => x));
        Assert.Equal(0, r.Manager.OpenCount);
        r.Blocker.Closed("steam.exe"); r.Blocker.Closed("steamwebhelper.exe");
        r.Blocker.Seen("steam.exe");                                // reopened: first warning again
        Assert.Equal(1, r.Manager.OpenCount);
        Assert.False(r.Manager.Get("Steam")!.IsExpired);
    }

    [AvaloniaFact]
    public void CheckingLastTask_Unlocks()
    {
        using var r = new Rig();
        r.Blocker.Seen("game.exe");
        Assert.Equal(1, r.Manager.OpenCount);
        r.Blocker.Seen("game.exe"); // duplicate: still one window
        Assert.Equal(1, r.Manager.OpenCount);
        TaskRules.SetDone(r.Task, DateOnly.FromDateTime(r.Now), true);
        r.State.DataChanged();
        Assert.Equal(0, r.Manager.OpenCount);
        Assert.False(r.State.IsLocked);
        Assert.Empty(r.Blocker.Killed);
    }

    [AvaloniaFact]
    public void Warning_Fechar_KillsApp()
    {
        using var r = new Rig();
        r.Blocker.Seen("game.exe");
        var w = r.Manager.Get("game.exe")!;
        Assert.Equal("Termine suas tarefas antes de abrir", w.Message);
        Assert.Equal(new[] { "Estudar" }, w.PendingTitles);
        Assert.Equal(new[] { 5, 10, 15, 20, 30, 45, 60 }, w.MinuteOptions);
        w.CloseButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(new[] { "game.exe" }, r.Blocker.Killed);
        Assert.Equal(0, r.Manager.OpenCount);
        r.Blocker.Seen("game.exe"); // reopened: first warning again
        Assert.Equal("Termine suas tarefas antes de abrir", r.Manager.Get("game.exe")!.Message);
    }

    [AvaloniaFact]
    public void Breaktime_ClosesWarning_ThenExpires()
    {
        using var r = new Rig();
        r.Blocker.Seen("game.exe");
        r.Manager.Get("game.exe")!.StartBreak(5);
        Assert.Equal(0, r.Manager.OpenCount);
        r.Now = r.Now.AddMinutes(6);
        r.State.CheckBreaks();
        Assert.Equal("Breaktime acabou, volte às tarefas", r.Manager.Get("game.exe")!.Message);
        r.State.CheckBreaks(); // not raised again
        Assert.Equal(1, r.Manager.OpenCount);
    }

    [AvaloniaFact]
    public void Expired_HasOnlyCloseButton()
    {
        using var r = new Rig();
        r.Blocker.Seen("game.exe");
        r.Manager.Get("game.exe")!.StartBreak(5);
        r.Now = r.Now.AddMinutes(6);
        r.State.CheckBreaks();
        var w = r.Manager.Get("game.exe")!;
        Assert.True(w.IsExpired);
        Assert.False(w.BreakButton.IsVisible);
        Assert.False(w.MinutesBox.IsVisible);
        Assert.True(w.CloseButton.IsVisible);
        w.CloseButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(new[] { "game.exe" }, r.Blocker.Killed);
    }

    [AvaloniaFact]
    public void AppClosed_ClosesItsWarning()
    {
        using var r = new Rig();
        r.Blocker.Seen("game.exe");
        r.Blocker.Closed("game.exe");
        Assert.Equal(0, r.Manager.OpenCount);
    }

    [AvaloniaFact]
    public void TasksView_CheckingTask_UpdatesStatusAndUnlocks()
    {
        using var r = new Rig();
        var v = new TasksView(r.State);
        Assert.Equal("Bloqueado", v.Status.Text);
        var box = v.TodayRows.Children.OfType<Avalonia.Controls.DockPanel>().SelectMany(d => d.Children).OfType<Avalonia.Controls.CheckBox>().Single();
        box.IsChecked = true;
        Assert.Equal("Liberado", v.Status.Text);
        Assert.False(r.State.IsLocked);
    }

    [Fact]
    public void RuleSummary_Formats()
    {
        Assert.Equal("Seg–Sex 21:30", RuleText.Summary(DateRule.Weekdays(), new TimeOnly(21, 30)));
        Assert.Equal("Todo dia 08:00", RuleText.Summary(DateRule.Every(Enum.GetValues<DayOfWeek>()), new TimeOnly(8, 0)));
        Assert.Equal("Fim de semana 10:00", RuleText.Summary(DateRule.Weekend(), new TimeOnly(10, 0)));
        Assert.Equal("Ter, Qui 19:00", RuleText.Summary(DateRule.Every(DayOfWeek.Thursday, DayOfWeek.Tuesday), new TimeOnly(19, 0)));
        Assert.Equal("12/11/2026 15:00", RuleText.Summary(DateRule.On(new DateOnly(2026, 11, 12)), new TimeOnly(15, 0)));
    }

    [Fact]
    public void BlockInput_NormalisesAndRefuses()
    {
        Assert.Equal("youtube.com", RuleText.NormalizeSite("https://www.YouTube.com/watch?v=1"));
        Assert.Equal("a.com", RuleText.NormalizeSite("a.com/x"));
        var d = new BlockList();
        Assert.Equal("Esse programa não pode ser bloqueado", RuleText.TryAddApp(d, "C:/x/Chrome.exe"));
        Assert.Null(RuleText.TryAddApp(d, "Steam"));
        Assert.Equal(new[] { "steam.exe" }, d.Apps);
    }
}

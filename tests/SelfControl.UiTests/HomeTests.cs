using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class HomeTests
{
    sealed class NoBlocker : IBlocker
    {
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
    }
    sealed class NoNotifier : INotifier { public void Show(string t, string m) { } }
    sealed class FakeAutostart : IAutostart { public bool Enabled { get; set; } }

    sealed class Rig : IDisposable
    {
        public DateTime Now = new(2026, 11, 3, 10, 0, 0);
        public string Dir = Path.Combine(Path.GetTempPath(), "sc-home-" + Guid.NewGuid().ToString("N")[..8]);
        public FakeAutostart Autostart = new();
        public AppState State;
        public Rig(bool firstRun = false) =>
            State = new AppState(new DataStore(Dir), new NoBlocker(), new NoNotifier(), Autostart, firstRun, () => Now);
        public void Dispose() { State.Dispose(); try { Directory.Delete(Dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void AddTextBlock_Persists()
    {
        using var r = new Rig();
        var v = new HomeView(r.State);
        v.AddBlock(HomeBlockKind.Text);
        Assert.Single(r.State.Data.Home);
        Assert.Equal(HomeBlockKind.Text, r.State.Data.Home[0].Kind);
        r.State.SaveNow();
        var loaded = new DataStore(r.Dir).Load();
        Assert.Single(loaded.Home);
        Assert.Equal(HomeBlockKind.Text, loaded.Home[0].Kind);
    }

    [AvaloniaFact]
    public void Reorder_ChangesOrder()
    {
        using var r = new Rig();
        var v = new HomeView(r.State);
        v.AddBlock(HomeBlockKind.Text);
        v.AddBlock(HomeBlockKind.TodayTasks);
        var first = r.State.Data.Home[0].Id;
        v.Move(0, 1);
        Assert.Equal(first, r.State.Data.Home[1].Id);
        Assert.Equal(HomeBlockKind.TodayTasks, r.State.Data.Home[0].Kind);
        v.Move(0, -1); // already first: no change
        Assert.Equal(HomeBlockKind.TodayTasks, r.State.Data.Home[0].Kind);
    }

    [AvaloniaFact]
    public void TodayTasks_Block_ChecksSameState()
    {
        using var r = new Rig();
        var task = new TaskItem { Title = "Estudar", TempDate = DateOnly.FromDateTime(r.Now) };
        r.State.Data.Tasks.Add(task);
        var home = new HomeView(r.State);
        home.AddBlock(HomeBlockKind.TodayTasks);
        var tasksView = new TasksView(r.State);
        var box = home.Panel.GetLogicalDescendants().OfType<CheckBox>().Single();
        box.IsChecked = true;
        Assert.True(TaskRules.IsDone(task, DateOnly.FromDateTime(r.Now)));
        Assert.Equal("Liberado", tasksView.Status.Text);
        Assert.False(r.State.IsLocked);
    }

    [AvaloniaFact]
    public void FirstRun_SeedsDefaultPage()
    {
        using var r = new Rig(firstRun: true);
        Assert.Equal(new[] { HomeBlockKind.Text, HomeBlockKind.TodayRoutine, HomeBlockKind.TodayTasks },
            r.State.Data.Home.Select(b => b.Kind).ToArray());
        Assert.Equal("Bem-vindo", r.State.Data.Home[0].Text);
    }

    [AvaloniaFact]
    public void RoutineBlock_HighlightsCurrent_AndLockStatusText()
    {
        using var r = new Rig();
        var routine = new Routine { Name = "A" };
        routine.Blocks.Add(new RoutineBlock { Title = "Manhã", Day = r.Now.DayOfWeek, StartMin = 8 * 60, EndMin = 12 * 60 });
        routine.Blocks.Add(new RoutineBlock { Title = "Tarde", Day = r.Now.DayOfWeek, StartMin = 14 * 60, EndMin = 18 * 60 + 30 });
        r.State.Data.Routines.Add(routine); r.State.Data.ActiveRoutineId = routine.Id;
        r.State.Data.Tasks.Add(new TaskItem { Title = "x", TempDate = DateOnly.FromDateTime(r.Now) });
        var v = new HomeView(r.State);
        v.AddBlock(HomeBlockKind.TodayRoutine);
        v.AddBlock(HomeBlockKind.LockStatus);
        var texts = v.Panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("08:00–12:00  Manhã", texts);
        Assert.Contains("14:00–18:30  Tarde", texts);
        Assert.Contains("Bloqueado — 1 tarefas pendentes", texts);
    }

    [AvaloniaFact]
    public void Import_InvalidFile_ReportsAndKeepsData()
    {
        using var r = new Rig();
        r.State.Data.Tasks.Add(new TaskItem { Title = "keep" });
        var bad = Path.Combine(r.Dir, "bad.json");
        Directory.CreateDirectory(r.Dir); File.WriteAllText(bad, "{ nope");
        Assert.Equal("Arquivo inválido", r.State.Import(bad));
        Assert.Single(r.State.Data.Tasks);
        var good = Path.Combine(r.Dir, "good.json");
        r.State.Export(good);
        r.State.Data.Tasks.Clear();
        Assert.Null(r.State.Import(good));
        Assert.Single(r.State.Data.Tasks);
    }
}

public class ImportTests
{
    sealed class NoBlocker : IBlocker
    {
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
    }
    sealed class NoNotifier : INotifier { public void Show(string t, string m) { } }
    sealed class FakeAutostart : IAutostart { public bool Enabled { get; set; } }

    [AvaloniaFact]
    public void Import_WithOpenRoutineView_ShowsAndEditsNewData()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-imp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var state = new AppState(new DataStore(dir), new NoBlocker(), new NoNotifier(), new FakeAutostart());
            var view = new RoutineView(state);
            var src = new AppData();
            var rt = new Routine { Name = "Importada" };
            rt.Blocks.Add(new RoutineBlock { Title = "B", Day = DayOfWeek.Monday, StartMin = 60, EndMin = 120 });
            src.Routines.Add(rt); src.ActiveRoutineId = rt.Id;
            var file = Path.Combine(dir, "in.json");
            new DataStore(dir).Export(src, file);
            Assert.Null(state.Import(file));
            var picker = view.GetLogicalDescendants().OfType<ComboBox>().First();
            Assert.Equal("Importada", ((Routine)picker.SelectedItem!).Name);
            var shown = (Routine)picker.SelectedItem!;
            Assert.Same(state.Data.Routines[0], shown);
            view.ActiveToggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Primitives.ToggleButton.ClickEvent));
            Assert.Equal(rt.Id, state.Data.ActiveRoutineId);
            state.Dispose();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void Import_OverUnreadableFile_ClearsLoadFailedAndSaves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-imp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "data.json");
            File.WriteAllText(path, "{}");
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var state = new AppState(new DataStore(dir), new NoBlocker(), new NoNotifier(), new FakeAutostart());
            Assert.True(state.LoadFailed);
            locked.Dispose();
            var src = new AppData(); src.Tasks.Add(new TaskItem { Title = "t" });
            var file = Path.Combine(dir, "in.json");
            new DataStore(dir).Export(src, file);
            Assert.Null(state.Import(file));
            Assert.False(state.LoadFailed);
            Assert.Single(new DataStore(dir).Load().Tasks);
            state.Dispose();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

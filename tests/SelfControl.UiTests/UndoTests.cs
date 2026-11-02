using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class UndoTests
{
    sealed class FakeBlocker : IBlocker
    {
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
    }
    sealed class FakeNotifier : INotifier { public void Show(string t, string m) { } }
    sealed class FakeAutostart : IAutostart { public bool Enabled { get; set; } }

    static (AppState s, string dir) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-undo-" + Guid.NewGuid().ToString("N")[..8]);
        return (new AppState(new DataStore(dir), new FakeBlocker(), new FakeNotifier(), new FakeAutostart()), dir);
    }

    [AvaloniaFact]
    public void Undo_RestoresPreviousState_StepByStep()
    {
        var (s, dir) = Make();
        try
        {
            Assert.False(s.CanUndo);
            s.Data.Tasks.Add(new TaskItem { Title = "A" }); s.DataChanged();
            s.Data.Tasks.Add(new TaskItem { Title = "B" }); s.DataChanged();
            Assert.True(s.Undo());
            Assert.Equal(new[] { "A" }, s.Data.Tasks.Select(t => t.Title));
            Assert.True(s.Undo());
            Assert.Empty(s.Data.Tasks);
            Assert.False(s.Undo());
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void Undo_InRoutineView_KeepsSameRoutineSelected()
    {
        var (s, dir) = Make();
        try
        {
            var a = new Routine { Name = "A" }; var b = new Routine { Name = "B" };
            s.Data.Routines.AddRange(new[] { a, b }); s.DataChanged();
            var view = new RoutineView(s);
            view.SelectRoutine(b.Id);
            b.Blocks.Add(new RoutineBlock { Title = "X", Day = DayOfWeek.Monday, StartMin = 600, EndMin = 660 }); s.DataChanged();
            s.Undo();
            Assert.Equal(b.Id, view.SelectedRoutineId);
            Assert.Empty(s.Data.Routines.Single(r => r.Id == b.Id).Blocks);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void DeleteSelected_RemovesSelectedBlock_AndIsUndoable()
    {
        var (s, dir) = Make();
        try
        {
            var r = new Routine { Name = "R" };
            r.Blocks.Add(new RoutineBlock { Title = "X", Day = DayOfWeek.Monday, StartMin = 600, EndMin = 660 });
            s.Data.Routines.Add(r); s.DataChanged();
            var view = new RoutineView(s);
            view.SelectRoutine(r.Id);
            Assert.False(view.DeleteSelected());          // nothing selected yet
            view.WeekGrid.Select(r.Blocks[0]);
            Assert.True(view.DeleteSelected());
            Assert.Empty(s.Data.Routines[0].Blocks);
            s.Undo();
            Assert.Single(s.Data.Routines[0].Blocks);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [AvaloniaFact]
    public void CopyPaste_AddsCopyAfterSelected_AndIsUndoable()
    {
        var (s, dir) = Make();
        try
        {
            var r = new Routine { Name = "R" };
            r.Blocks.Add(new RoutineBlock { Title = "X", Day = DayOfWeek.Monday, StartMin = 600, EndMin = 660, Color = "#123456", Note = "Perna" });
            s.Data.Routines.Add(r); s.DataChanged();
            var view = new RoutineView(s);
            view.SelectRoutine(r.Id);
            Assert.False(view.CopySelected());            // nothing selected yet
            view.WeekGrid.Select(r.Blocks[0]);
            Assert.True(view.CopySelected());
            Assert.True(view.Paste());
            Assert.True(view.Paste());                     // each paste goes after the last one
            var b = s.Data.Routines[0].Blocks;
            Assert.Equal(3, b.Count);
            Assert.Equal(new[] { 600, 660, 720 }, b.Select(x => x.StartMin));
            Assert.All(b, x => { Assert.Equal("X", x.Title); Assert.Equal("Perna", x.Note); Assert.Equal("#123456", x.Color); Assert.Equal(DayOfWeek.Monday, x.Day); });
            Assert.Equal(3, b.Select(x => x.Id).Distinct().Count());
            s.Undo();
            Assert.Equal(2, s.Data.Routines[0].Blocks.Count);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }
}

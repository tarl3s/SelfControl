using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class ProgramsAndDeleteTests
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

    [AvaloniaFact]
    public void DeletingSelectedRoutine_InAShownWindow_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-del-" + Guid.NewGuid().ToString("N")[..8]);
        var s = new AppState(new DataStore(dir), new FakeBlocker(), new FakeNotifier(), new FakeAutostart());
        try
        {
            var a = new Routine { Name = "A" }; var b = new Routine { Name = "B" };
            s.Data.Routines.AddRange(new[] { a, b }); s.DataChanged();
            var view = new RoutineView(s);
            var w = new Window { Content = view }; w.Show();
            view.SelectRoutine(b.Id);
            RoutineOps.DeleteRoutine(s.Data, b.Id); s.DataChanged();   // what the Excluir button does
            Assert.Equal(a.Id, view.SelectedRoutineId);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void AddProgram_AddsAllExes_GroupsByName_SkipsProtected()
    {
        var list = new BlockList();
        Assert.Null(RuleText.TryAddProgram(list, new InstalledApp("Steam", "Valve", new[] { "steam.exe", "steamwebhelper.exe", "explorer.exe" }, null)));
        Assert.Equal(new[] { "steam.exe", "steamwebhelper.exe" }, list.Apps);
        list.Apps.Add("game.exe");
        var groups = RuleText.AppGroups(list);
        Assert.Equal(new[] { "Steam", "game.exe" }, groups.Select(g => g.Label));
        Assert.Equal(2, groups[0].Exes.Count);
    }

    [Fact]
    public void PickerFilter_MatchesNameOrPublisher()
    {
        var apps = new[] { new InstalledApp("Discord", "Discord Inc.", new[] { "discord.exe" }, null), new InstalledApp("Steam", "Valve", new[] { "steam.exe" }, null) };
        Assert.Equal(new[] { "Steam" }, AppPickerDialog.Filter(apps, "valve").Select(a => a.Name));
        Assert.Equal(2, AppPickerDialog.Filter(apps, " ").Count());
    }
}

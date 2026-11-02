using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;

namespace SelfControl.UiTests;

public class ShellTests
{
    [AvaloniaFact]
    public void Nav_Switches_Views()
    {
        var w = new MainWindow();
        try
        {
            w.Show();
            var nav = w.FindControl<ListBox>("Nav")!;
            var host = w.FindControl<ContentControl>("Host")!;
            Assert.IsType<HomeView>(host.Content);
            nav.SelectedIndex = 1; Assert.IsType<RoutineView>(host.Content);
            nav.SelectedIndex = 2; Assert.IsType<EventsView>(host.Content);
            nav.SelectedIndex = 3; Assert.IsType<TasksView>(host.Content);
        }
        finally { w.AllowClose = true; w.Close(); }
    }

    [AvaloniaFact]
    public void Close_HidesInsteadOfExit()
    {
        var w = new MainWindow();
        try
        {
            w.Show();
            w.Close();
            Assert.False(w.IsVisible);
            w.Show();
            Assert.True(w.IsVisible);
        }
        finally { w.AllowClose = true; w.Close(); }
    }
}

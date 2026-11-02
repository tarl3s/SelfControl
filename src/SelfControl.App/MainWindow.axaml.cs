using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SelfControl.App.Views;

namespace SelfControl.App;

public partial class MainWindow : Window
{
    readonly Control[] _views;
    readonly ListBox _nav;
    readonly ContentControl _host;

    /// <summary>Set by the Sair menu so Close really closes; otherwise closing only hides to the tray.</summary>
    public bool AllowClose { get; set; }

    public MainWindow() : this(null) { }

    public MainWindow(AppState? state)
    {
        AvaloniaXamlLoader.Load(this);
        _views = new Control[] { new HomeView(state), new RoutineView(state), new EventsView(state), new TasksView(state) };
        _nav = this.FindControl<ListBox>("Nav")!;
        _host = this.FindControl<ContentControl>("Host")!;
        var banner = this.FindControl<Border>("LoadFailedBanner")!;
        banner.IsVisible = state?.LoadFailed == true;
        if (state != null) state.ViewsChanged += () => banner.IsVisible = state.LoadFailed;
        if (state != null) this.FindControl<Button>("Gear")!.Click += async (_, _) => await SettingsDialog.Show(this, state);
        else this.FindControl<Button>("Gear")!.IsEnabled = false;
        _nav.SelectionChanged += (_, _) => ShowSelected();
        ShowSelected();
    }

    void ShowSelected() => _host.Content = _views[Math.Max(0, _nav.SelectedIndex)];

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason is WindowCloseReason.WindowClosing or WindowCloseReason.Undefined)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}

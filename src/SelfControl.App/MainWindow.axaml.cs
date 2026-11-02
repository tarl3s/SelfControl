using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SelfControl.App.Views;

namespace SelfControl.App;

public partial class MainWindow : Window
{
    readonly Control[] _views;
    readonly ListBox _nav;
    readonly ContentControl _host;
    readonly AppState? _state;

    /// <summary>Set by the Sair menu so Close really closes; otherwise closing only hides to the tray.</summary>
    public bool AllowClose { get; set; }

    public MainWindow() : this(null) { }

    public MainWindow(AppState? state)
    {
        AvaloniaXamlLoader.Load(this);
        _state = state;
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

    /// <summary>Ctrl+Z undoes the last change; Delete removes the selected routine block. Text boxes keep their own keys.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _state == null || FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control) e.Handled = _state.Undo();
        else if (e.Key == Key.Delete && _host.Content is RoutineView rv) e.Handled = rv.DeleteSelected();
    }

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

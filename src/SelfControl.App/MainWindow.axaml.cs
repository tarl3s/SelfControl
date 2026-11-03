using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SelfControl.App.Views;
using SelfControl.Core;

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
        this.FindControl<TextBlock>("LoadFailedText")!.Text = L.T("Could not read your data — changes will not be saved");
        this.FindControl<Button>("Gear")!.Content = L.T("⚙ Settings");
        this.FindControl<ListBoxItem>("NavHome")!.Content = L.T("Home");
        this.FindControl<ListBoxItem>("NavRoutine")!.Content = L.T("Routine");
        this.FindControl<ListBoxItem>("NavEvents")!.Content = L.T("Events");
        this.FindControl<ListBoxItem>("NavTasks")!.Content = L.T("Tasks");
        this.FindControl<ListBoxItem>("NavUsage")!.Content = L.T("Usage");
        _views = new Control[] { new HomeView(state), new RoutineView(state), new EventsView(state), new TasksView(state), new UsageView(state) };
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

    /// <summary>Ctrl+Z undoes the last change; Delete removes and Ctrl+C / Ctrl+V copy and paste the selected routine block. Text boxes keep their own keys.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _state == null || FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control) e.Handled = _state.Undo();
        else if (e.Key == Key.Delete && _host.Content is RoutineView rv) e.Handled = rv.DeleteSelected();
        else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && _host.Content is RoutineView rc) e.Handled = rc.CopySelected();
        else if (e.Key == Key.V && e.KeyModifiers == KeyModifiers.Control && _host.Content is RoutineView rp) e.Handled = rp.Paste();
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

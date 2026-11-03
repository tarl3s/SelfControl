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
        _views = new Control[] { new HomeView(state), new RoutineView(state), new EventsView(state), new TasksView(state) };
        _nav = this.FindControl<ListBox>("Nav")!;
        _host = this.FindControl<ContentControl>("Host")!;
        var banner = this.FindControl<Border>("LoadFailedBanner")!;
        banner.IsVisible = state?.LoadFailed == true;
        if (state != null) state.ViewsChanged += () => banner.IsVisible = state.LoadFailed;
        if (state != null) this.FindControl<Button>("Gear")!.Click += async (_, _) => await SettingsDialog.Show(this, state);
        else this.FindControl<Button>("Gear")!.IsEnabled = false;
        var usageText = this.FindControl<TextBlock>("UsageText")!;
        this.FindControl<TextBlock>("UsageLabel")!.Text = L.T("Usage time today");
        var usagePanel = this.FindControl<StackPanel>("UsagePanel")!;
        void ShowUsage()
        {
            if (state == null) return;
            usageText.Text = Usage.Format(state.TodayUsage);
            var today = DateOnly.FromDateTime(DateTime.Now);
            ToolTip.SetTip(usagePanel, L.T("Counts only while you use the computer (not locked, asleep or 15 minutes without use).") + "\n\n" +
                string.Join("\n", Enumerable.Range(0, 7).Select(i => today.AddDays(-i)).Select(d => $"{d:ddd dd/MM}: {Usage.Format(Usage.On(state.Data, d))}")));
        }
        usagePanel.IsVisible = state != null;
        ShowUsage();
        if (state != null) { state.UsageChanged += ShowUsage; state.ViewsChanged += ShowUsage; }
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>
/// Full-cover, always-on-top warning for a blocked app. The app behind cannot be used: the window covers the screen
/// and re-asserts topmost every 500 ms. Only the buttons (or the lock lifting) can close it.
/// </summary>
public sealed class BlockWarningWindow : Window
{
    public const string FirstText = "Termine suas tarefas antes de abrir";
    public const string ExpiredText = "Breaktime acabou, volte às tarefas";
    public static readonly int[] Minutes = { 5, 10, 15, 20, 30, 45, 60 };

    readonly AppState _state;
    readonly DispatcherTimer _top = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool _dismissed;

    public string Exe { get; }
    public bool IsExpired { get; }
    public string Message => IsExpired ? ExpiredText : FirstText;
    public IReadOnlyList<string> PendingTitles { get; }
    public IReadOnlyList<int> MinuteOptions => Minutes;
    public ComboBox MinutesBox { get; } = new() { ItemsSource = Minutes, SelectedIndex = 1, MinWidth = 90 };
    public Button BreakButton { get; } = new() { Content = "Breaktime", Padding = new Thickness(18, 8) };
    public Button CloseButton { get; } = new() { Content = "Fechar", Padding = new Thickness(18, 8) };

    public BlockWarningWindow(AppState state, string exe, BlockPhase phase)
    {
        _state = state; Exe = exe; IsExpired = phase == BlockPhase.Expired;
        var today = DateOnly.FromDateTime(state.Now);
        PendingTitles = TaskRules.Today(state.Data, today).Where(t => !TaskRules.IsDone(t, today)).Select(t => t.Title).ToList();

        SystemDecorations = SystemDecorations.None;
        Topmost = true; ShowInTaskbar = false; CanResize = false;
        Background = new SolidColorBrush(Color.Parse("#F2000000"));
        WindowStartupLocation = WindowStartupLocation.Manual;

        var panel = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 520 };
        panel.Children.Add(new TextBlock { Text = Message, FontSize = 30, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = exe, Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")), HorizontalAlignment = HorizontalAlignment.Center });
        if (!IsExpired)
        {
            foreach (var t in PendingTitles)
                panel.Children.Add(new TextBlock { Text = "•  " + t, FontSize = 16 });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            row.Children.Add(new TextBlock { Text = "Minutos", VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(MinutesBox);
            BreakButton.Classes.Add("primary");
            row.Children.Add(BreakButton);
            row.Children.Add(CloseButton);
            panel.Children.Add(row);
        }
        else
        {
            BreakButton.IsVisible = false; MinutesBox.IsVisible = false;
            CloseButton.Classes.Add("primary");
            CloseButton.HorizontalAlignment = HorizontalAlignment.Center;
            panel.Children.Add(CloseButton);
        }
        Content = panel;

        BreakButton.Click += (_, _) => StartBreak(MinutesBox.SelectedItem is int m ? m : 10);
        CloseButton.Click += (_, _) => _state.CloseApp(Exe);
        _top.Tick += (_, _) => Reassert();
        Opened += (_, _) => { Cover(); _top.Start(); Reassert(); };
    }

    public void StartBreak(int minutes) => _state.StartAppBreak(Exe, minutes);

    void Cover()
    {
        var screen = Screens.Primary;
        if (screen != null)
        {
            Position = screen.Bounds.Position;
            var s = screen.Scaling <= 0 ? 1 : screen.Scaling;
            Width = screen.Bounds.Width / s; Height = screen.Bounds.Height / s;
        }
        WindowState = WindowState.FullScreen;
    }

    void Reassert()
    {
        if (_dismissed) return;
        Topmost = false; Topmost = true;
        if (!IsActive) Activate();
    }

    /// <summary>Closes for real (Fechar, Breaktime, app closed, or the lock lifted).</summary>
    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true; _top.Stop();
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_dismissed) e.Cancel = true; // Alt+F4 does not get past the warning
        base.OnClosing(e);
    }
}

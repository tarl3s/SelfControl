using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Uso screen: time of real use per day (a chosen week, or the last 4 weeks) and the programs and sites used most.</summary>
public class UsageView : UserControl
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    static readonly IBrush Card = new SolidColorBrush(Color.Parse("#1A1A1A"));
    readonly AppState? _state;
    readonly AppData _fallback = new();
    readonly Func<DateOnly> _today;
    readonly TextBlock _total = new() { FontSize = 28, FontWeight = FontWeight.SemiBold };
    readonly TextBlock _average = new() { Foreground = Dim };
    internal readonly ComboBox Period = new() { MinWidth = 200 };
    internal readonly UsageChart Chart = new() { Height = 220 };
    internal readonly StackPanel Apps = new() { Spacing = 4 };
    internal readonly StackPanel Sites = new() { Spacing = 4 };

    public UsageView() : this(null) { }

    /// <param name="today">For tests; defaults to the real date.</param>
    public UsageView(AppState? state, Func<DateOnly>? today = null)
    {
        _state = state;
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        // 0 = this week, 1..3 = weeks ago, 4 = the last 4 weeks
        Period.ItemsSource = new[] { L.T("This week"), L.T("Last week"), L.T("2 weeks ago"), L.T("3 weeks ago"), L.T("Last 4 weeks") };
        Period.SelectedIndex = 0;
        Period.SelectionChanged += (_, _) => Rebuild();

        var head = new DockPanel();
        head.Children.Add(RoutineDialogs.Docked(Period, Dock.Right));
        head.Children.Add(new TextBlock { Text = L.T("Usage"), FontSize = 24, FontWeight = FontWeight.SemiBold });

        var lists = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        lists.Children.Add(Section(L.T("Programs"), Apps, 0));
        lists.Children.Add(Section(L.T("Sites"), Sites, 2));

        var body = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel { Children = { _total, _average } },
                new Border { Background = Card, CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Child = Chart },
                lists,
                new TextBlock { Text = L.T("Counts only while you use the computer (not locked, asleep or 15 minutes without use)."), Foreground = Dim, TextWrapping = TextWrapping.Wrap },
            }
        };
        var root = new DockPanel { Margin = new Thickness(24) };
        root.Children.Add(RoutineDialogs.Docked(new Border { Margin = new Thickness(0, 0, 0, 12), Child = head }, Dock.Top));
        root.Children.Add(new ScrollViewer { Content = body });
        Content = root;
        Rebuild();
        if (state != null) { state.UsageChanged += Rebuild; state.ViewsChanged += Rebuild; }
    }

    static Control Section(string title, StackPanel list, int col)
    {
        var b = new Border
        {
            Background = Card, CornerRadius = new CornerRadius(6), Padding = new Thickness(12),
            Child = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold }, list } },
        };
        Grid.SetColumn(b, col);
        return b;
    }

    AppData Data => _state?.Data ?? _fallback;

    /// <summary>The days of the chosen period, oldest first.</summary>
    internal List<DateOnly> Days()
    {
        var monday = Usage.WeekStart(_today());
        int sel = Math.Max(0, Period.SelectedIndex);
        if (sel == 4) return Enumerable.Range(0, 7 * Usage.Weeks).Select(i => monday.AddDays(-7 * (Usage.Weeks - 1) + i)).ToList();
        return Enumerable.Range(0, 7).Select(i => monday.AddDays(-7 * sel + i)).ToList();
    }

    void Rebuild()
    {
        var today = _today();
        var days = Days();
        bool month = days.Count > 7;
        var bars = days.Select(d => (month ? d.ToString("dd/MM") : DayName(d.DayOfWeek), d > today ? TimeSpan.Zero : Usage.On(Data, d), d == today)).ToList();
        Chart.Bars = bars;
        var total = TimeSpan.FromSeconds(bars.Sum(b => b.Item2.TotalSeconds));
        int counted = days.Count(d => d <= today);
        _total.Text = Usage.Format(total);
        _average.Text = L.F("Daily average: {0}", Usage.Format(counted == 0 ? TimeSpan.Zero : total / counted));
        Fill(Apps, Usage.Top(Data.UsageApps, days), L.T("Nothing yet."));
        Fill(Sites, Usage.Top(Data.UsageSites, days), L.T("Nothing yet. Sites need the browser extension."));
    }

    static string DayName(DayOfWeek d) => L.T(d switch
    {
        DayOfWeek.Monday => "Mon", DayOfWeek.Tuesday => "Tue", DayOfWeek.Wednesday => "Wed", DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri", DayOfWeek.Saturday => "Sat", _ => "Sun",
    });

    static void Fill(StackPanel list, List<(string Name, TimeSpan Time)> top, string empty)
    {
        list.Children.Clear();
        if (top.Count == 0) { list.Children.Add(new TextBlock { Text = empty, Foreground = Dim, TextWrapping = TextWrapping.Wrap }); return; }
        double max = Math.Max(1, top[0].Time.TotalSeconds);
        foreach (var (name, t) in top.Take(10))
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
            row.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis });
            var time = new TextBlock { Text = Usage.Format(t), Foreground = Dim, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(time, 1); row.Children.Add(time);
            double f = t.TotalSeconds / max;
            var track = new Grid { Margin = new Thickness(0, 3, 0, 4) };
            track.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(0.001, f), GridUnitType.Star)));
            track.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(0.001, 1 - f), GridUnitType.Star)));
            track.Children.Add(new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Brushes.White });
            Grid.SetRow(track, 1); Grid.SetColumnSpan(track, 2); row.Children.Add(track);
            list.Children.Add(row);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>When something happens: weekday chips + shortcuts, or one specific date, and (optionally) a time.</summary>
public sealed class RuleEditor : UserControl
{
    readonly ToggleButton[] _chips = new ToggleButton[7];
    readonly ToggleButton _specific = new() { Content = L.T("Specific date") };
    readonly CalendarDatePicker _date = new() { SelectedDate = DateTime.Today };
    readonly TimePicker? _time;
    readonly StackPanel _chipRow = new() { Orientation = Orientation.Horizontal, Spacing = 6 };

    public RuleEditor(DateRule rule, TimeOnly? time)
    {
        var shortcuts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void Short(string text, DateRule r)
        {
            var b = new Button { Content = text };
            b.Click += (_, _) => { _specific.IsChecked = false; SetDays(r.Days); Sync(); };
            shortcuts.Children.Add(b);
        }
        Short(L.T("Mon–Fri"), DateRule.Weekdays());
        Short(L.T("Every day"), DateRule.Every(Enum.GetValues<DayOfWeek>()));
        Short(L.T("Weekend"), DateRule.Weekend());

        for (int i = 0; i < 7; i++)
            _chipRow.Children.Add(_chips[i] = new ToggleButton { Content = RuleText.DayName(RuleText.WeekOrder[i]), MinWidth = 48 });

        _specific.IsCheckedChanged += (_, _) => Sync();
        var panel = new StackPanel { Spacing = 8, Children = { shortcuts, _chipRow, _specific, _date } };
        if (time is { } t)
        {
            _time = new TimePicker { SelectedTime = t.ToTimeSpan(), ClockIdentifier = "24HourClock", MinuteIncrement = 1 };
            panel.Children.Add(new TextBlock { Text = L.T("Time") });
            panel.Children.Add(_time);
        }
        Content = panel;

        if (rule.Date is { } d) { _specific.IsChecked = true; _date.SelectedDate = d.ToDateTime(TimeOnly.MinValue); }
        else SetDays(rule.Days);
        Sync();
    }

    void SetDays(IEnumerable<DayOfWeek> days)
    {
        var set = days.ToHashSet();
        for (int i = 0; i < 7; i++) _chips[i].IsChecked = set.Contains(RuleText.WeekOrder[i]);
    }

    void Sync()
    {
        var specific = _specific.IsChecked == true;
        _date.IsVisible = specific;
        _chipRow.IsEnabled = !specific;
    }

    public DateRule? Rule
    {
        get
        {
            if (_specific.IsChecked == true)
                return _date.SelectedDate is { } d ? DateRule.On(DateOnly.FromDateTime(d)) : null;
            var days = RuleText.WeekOrder.Where((_, i) => _chips[i].IsChecked == true).ToArray();
            return days.Length == 0 ? null : DateRule.Every(days);
        }
    }

    public TimeOnly Time => _time?.SelectedTime is { } t ? TimeOnly.FromTimeSpan(t) : TimeOnly.MinValue;
}

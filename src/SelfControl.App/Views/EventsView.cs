using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Eventos screen: reminders (message + time + rule), each with an on/off toggle.</summary>
public class EventsView : UserControl
{
    readonly AppState? _state;
    readonly AppData _fallback = new();
    readonly StackPanel _list = new() { Spacing = 6 };

    public EventsView() : this(null) { }

    public EventsView(AppState? state)
    {
        _state = state;
        var add = RoutineDialogs.Btn(L.T("＋ New event"), true);
        add.Click += (_, _) => Edit(null);
        var root = new DockPanel { Margin = new Thickness(24) };
        root.Children.Add(RoutineDialogs.Docked(new TextBlock { Text = L.T("Events"), FontSize = 24, FontWeight = FontWeight.SemiBold }, Dock.Top));
        root.Children.Add(RoutineDialogs.Docked(new StackPanel { Margin = new Thickness(0, 12), Children = { add }, HorizontalAlignment = HorizontalAlignment.Left }, Dock.Top));
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
        Rebuild();
        if (state != null) state.ViewsChanged += Rebuild;
    }

    // The state may replace its data object (ReplaceData), so always read the current one.
    AppData Data => _state?.Data ?? _fallback;

    void Changed() { _state?.DataChanged(); Rebuild(); }

    void Rebuild()
    {
        _list.Children.Clear();
        if (Data.Events.Count == 0)
            _list.Children.Add(new TextBlock { Text = L.T("No events. Create a reminder with a time and days."), Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")) });
        foreach (var ev in Data.Events.OrderBy(e => e.Time).ToList())
        {
            var e = ev;
            var on = new ToggleSwitch { IsChecked = e.Enabled, OnContent = "", OffContent = "" };
            on.IsCheckedChanged += (_, _) => { e.Enabled = on.IsChecked == true; _state?.DataChanged(); };
            var edit = RoutineDialogs.Btn(L.T("Edit")); edit.Click += (_, _) => Edit(e);
            var del = RoutineDialogs.Btn(L.T("Delete"));
            del.Click += (_, _) => { Data.Events.Remove(e); Changed(); };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children =
            {
                new TextBlock { Text = e.Message, FontSize = 16 },
                new TextBlock { Text = RuleText.Summary(e.Rule, e.Time), Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")) },
            } };
            var row = new DockPanel { LastChildFill = true };
            row.Children.Add(RoutineDialogs.Docked(on, Dock.Left));
            row.Children.Add(RoutineDialogs.Docked(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { edit, del } }, Dock.Right));
            row.Children.Add(text);
            _list.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#1A1A1A")), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8), Child = row });
        }
    }

    async void Edit(ReminderEvent? ev)
    {
        if (TopLevel.GetTopLevel(this) is not Window w) return;
        var isNew = ev == null;
        ev ??= new ReminderEvent { Time = new TimeOnly(9, 0) };
        var msg = new TextBox { Text = ev.Message, MinWidth = 300, Watermark = L.T("Message") };
        var rule = new RuleEditor(ev.Rule, ev.Time);
        var save = RoutineDialogs.Btn(L.T("Save"), true); var cancel = RoutineDialogs.Btn(L.T("Cancel"));
        var error = new TextBlock { IsVisible = false };
        var win = RoutineDialogs.Shell(isNew ? L.T("New event") : L.T("Edit event"), 420, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 10,
            Children = { new TextBlock { Text = L.T("Message") }, msg, rule, error, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } } }
        });
        bool ok = false;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(msg.Text)) { error.Text = L.T("Write the message"); error.IsVisible = true; return; }
            if (rule.Rule is not { } r) { error.Text = L.T("Choose the days or a date"); error.IsVisible = true; return; }
            ev.Message = msg.Text.Trim(); ev.Rule = r; ev.Time = rule.Time; ok = true; win.Close();
        };
        cancel.Click += (_, _) => win.Close();
        await win.ShowDialog(w);
        if (!ok) return;
        if (isNew) Data.Events.Add(ev);
        Changed();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Rotina screen: routine picker + actions on top, the weekly grid below.</summary>
public class RoutineView : UserControl
{
    static readonly string[] DayNames = { "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom" };
    readonly AppState? _state;
    readonly AppData _data;
    readonly ComboBox _picker = new() { MinWidth = 180, DisplayMemberBinding = new Avalonia.Data.Binding("Name") };
    readonly ToggleButton _active = new() { Content = "Ativa" };
    readonly RoutineGrid _grid = new();
    readonly ScrollViewer _scroll;
    readonly Button _rename, _dup, _del;
    bool _busy;

    public RoutineView() : this(null) { }

    public RoutineView(AppState? state)
    {
        _state = state;
        _data = state?.Data ?? new AppData();
        Button B(string t, Action a) { var b = new Button { Content = t }; b.Click += (_, _) => a(); return b; }
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(16, 12) };
        _rename = B("Renomear", Rename); _dup = B("Duplicar", Duplicate); _del = B("Excluir", Delete);
        var sched = B("Agendar semanas…", Schedule);
        foreach (var c in new Control[] { _picker, B("＋ Nova", New), _rename, _dup, _del, _active, sched }) bar.Children.Add(c);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition(RoutineGrid.LeftW, GridUnitType.Pixel));
        for (int i = 0; i < 7; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var t = new TextBlock { Text = DayNames[i], HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")), Margin = new Thickness(0, 4) };
            Grid.SetColumn(t, i + 1);
            header.Children.Add(t);
        }
        _grid.Height = 24 * RoutineGrid.HourH;
        _grid.HorizontalAlignment = HorizontalAlignment.Stretch;
        _scroll = new ScrollViewer { Content = _grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var root = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
        DockPanel.SetDock(bar, Dock.Top); DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(bar); root.Children.Add(header); root.Children.Add(_scroll);
        Content = root;

        _picker.ItemsSource = _data.Routines;
        _picker.SelectionChanged += (_, _) => { if (!_busy) ShowSelected(); };
        _active.Click += (_, _) => SetActive();
        _grid.Changed += Save;
        _grid.EditRequested += async b =>
        {
            if (TopLevel.GetTopLevel(this) is not Window w || _grid.Routine == null) return;
            if (await RoutineDialogs.EditBlock(w, _grid.Routine, b)) { _grid.Refresh(deselect: !_grid.Routine.Blocks.Contains(b)); Save(); }
        };
        if (_data.Routines.Count > 0)
            _picker.SelectedItem = _data.Routines.FirstOrDefault(r => r.Id == _data.ActiveRoutineId) ?? _data.Routines[0];
        ShowSelected();

        // Opens scrolled to about the current hour; the white time line moves once a minute.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (_, _) => _grid.Refresh();
        AttachedToVisualTree += (_, _) =>
        {
            timer.Start();
            Dispatcher.UIThread.Post(() => _scroll.Offset = new Vector(0, Math.Max(0, (DateTime.Now.Hour - 1) * RoutineGrid.HourH)), DispatcherPriority.Loaded);
        };
        DetachedFromVisualTree += (_, _) => timer.Stop();
        if (_state != null) _state.ViewsChanged += SyncPicker;
    }

    Routine? Current => _picker.SelectedItem as Routine;
    Window? Owner => TopLevel.GetTopLevel(this) as Window;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    void Save() => _state?.DataChanged();

    void ShowSelected()
    {
        var r = Current;
        _grid.Routine = r;
        _rename.IsEnabled = _dup.IsEnabled = _del.IsEnabled = _active.IsEnabled = r != null;
        _active.IsChecked = r != null && _data.ActiveRoutineId == r.Id;
    }

    /// <summary>Keeps the picker in step if routines changed elsewhere (also after our own edits); never saves.</summary>
    void SyncPicker()
    {
        _busy = true;
        var cur = Current;
        _picker.ItemsSource = null; _picker.ItemsSource = _data.Routines;
        _picker.SelectedItem = cur != null && _data.Routines.Contains(cur) ? cur : _data.Routines.FirstOrDefault();
        _busy = false;
        var shown = Current;
        if (shown != _grid.Routine) ShowSelected();
        else { _active.IsChecked = shown != null && _data.ActiveRoutineId == shown.Id; _grid.Refresh(); }
    }

    void Reselect(Routine? r)
    {
        SyncPicker();
        if (r != null) { _picker.SelectedItem = r; }
    }

    void SetActive()
    {
        if (Current is not { } r) return;
        RoutineResolver.SetManual(_data, r.Id, Today);
        _active.IsChecked = true;
        Save();
    }

    async void New()
    {
        if (Owner is not { } w) return;
        var name = await RoutineDialogs.Prompt(w, "Nova rotina", "Nome da rotina", "Nova rotina");
        if (name == null) return;
        var r = new Routine { Name = name };
        _data.Routines.Add(r);
        if (_data.ActiveRoutineId == null) _data.ActiveRoutineId = r.Id;
        Reselect(r); Save();
    }

    async void Rename()
    {
        if (Owner is not { } w || Current is not { } r) return;
        var name = await RoutineDialogs.Prompt(w, "Renomear rotina", "Nome da rotina", r.Name);
        if (name == null) return;
        r.Name = name;
        Reselect(r); Save();
    }

    void Duplicate()
    {
        if (Current is not { } r) return;
        var c = new Routine { Name = r.Name + " (cópia)" };
        foreach (var b in r.Blocks)
            c.Blocks.Add(new RoutineBlock { Title = b.Title, Day = b.Day, StartMin = b.StartMin, EndMin = b.EndMin, Color = b.Color });
        _data.Routines.Add(c);
        Reselect(c); Save();
    }

    void Delete()
    {
        if (Current is not { } r) return;
        RoutineOps.DeleteRoutine(_data, r.Id);
        Reselect(null); Save();
    }

    async void Schedule()
    {
        if (Owner is not { } w) return;
        await RoutineDialogs.Schedule(w, _data, Save);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Tarefas screen: today's checklist + lock status, and the block list (apps and sites).</summary>
public class TasksView : UserControl
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    readonly AppState? _state;
    readonly AppData _fallback = new();
    readonly StackPanel _today = new() { Spacing = 4 };
    readonly StackPanel _apps = new() { Spacing = 4 };
    readonly StackPanel _sites = new() { Spacing = 4 };
    readonly TextBlock _status = new() { FontSize = 16 };
    readonly TextBlock _error = new() { IsVisible = false };

    public TasksView() : this(null) { }

    public TasksView(AppState? state)
    {
        _state = state;
        var addTask = RoutineDialogs.Btn("＋ Tarefa", true);
        addTask.Click += (_, _) => NewTask();

        var site = new TextBox { Watermark = "youtube.com", MinWidth = 220 };
        var addSite = RoutineDialogs.Btn("Adicionar");
        void AddSite() { Report(RuleText.TryAddSite(Data.BlockList, site.Text ?? "")); site.Text = ""; Changed(); }
        addSite.Click += (_, _) => AddSite();
        site.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) AddSite(); };

        var addApp = RoutineDialogs.Btn("Escolher programa…");
        addApp.IsEnabled = state?.Apps != null;
        addApp.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not Window w || _state?.Apps is not { } catalog) return;
            if (await AppPickerDialog.Show(w, catalog) is { } app) { Report(RuleText.TryAddProgram(Data.BlockList, app)); Changed(); }
        };
        var browse = RoutineDialogs.Btn("Procurar .exe…");
        browse.Click += async (_, _) => await Browse();

        var left = new StackPanel { Spacing = 8, Children =
        {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { new TextBlock { Text = "Hoje", FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, addTask } },
            _status, _today,
        } };
        var right = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "Lista de bloqueio", FontSize = 20, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "Programas", Foreground = Dim },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { addApp, browse } },
            _apps,
            new TextBlock { Text = "Sites", Foreground = Dim, Margin = new Thickness(0, 8, 0, 0) },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { site, addSite } },
            _sites, _error,
        } };
        var grid = new Grid { Margin = new Thickness(24) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(24, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(right);
        Content = new ScrollViewer { Content = grid };
        Rebuild();
        if (state != null) state.ViewsChanged += Rebuild;
    }

    AppData Data => _state?.Data ?? _fallback;
    DateOnly Today => DateOnly.FromDateTime(_state?.Now ?? DateTime.Now);
    internal StackPanel TodayRows => _today;
    internal TextBlock Status => _status;

    void Changed() { _state?.DataChanged(); Rebuild(); }

    void Report(string? error)
    {
        _error.Text = error ?? ""; _error.IsVisible = error != null;
    }

    async Task Browse()
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolha o programa", AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Programas") { Patterns = new[] { "*.exe" } } },
        });
        if (files.Count == 0) return;
        Report(RuleText.TryAddApp(Data.BlockList, files[0].Path.LocalPath));
        Changed();
    }

    void Rebuild()
    {
        var today = Today;
        var tasks = TaskRules.Today(Data, today);
        _today.Children.Clear();
        if (tasks.Count == 0) _today.Children.Add(new TextBlock { Text = "Nenhuma tarefa para hoje.", Foreground = Dim });
        foreach (var t in tasks)
        {
            var task = t;
            var box = new CheckBox { Content = task.Title, IsChecked = TaskRules.IsDone(task, today) };
            box.IsCheckedChanged += (_, _) =>
            {
                TaskRules.SetDone(task, today, box.IsChecked == true);
                Changed();
            };
            var del = RoutineDialogs.Btn("✕");
            del.Click += (_, _) => { Data.Tasks.Remove(task); Changed(); };
            var row = new DockPanel();
            row.Children.Add(RoutineDialogs.Docked(del, Dock.Right));
            if (task.Rule != null) row.Children.Add(RoutineDialogs.Docked(new TextBlock { Text = RuleText.Days(task.Rule), Foreground = Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, Dock.Right));
            row.Children.Add(box);
            _today.Children.Add(row);
        }
        _status.Text = TaskRules.IsLocked(Data, today) ? "Bloqueado" : "Liberado";

        FillApps();
        Fill(_sites, Data.BlockList.Sites, s => { Data.BlockList.Sites.Remove(s); Changed(); });
    }

    /// <summary>One row per program ("Steam", with its exes underneath); Remover takes out all of them.</summary>
    void FillApps()
    {
        _apps.Children.Clear();
        var list = Data.BlockList;
        if (list.Apps.Count == 0) _apps.Children.Add(new TextBlock { Text = "Nenhum.", Foreground = Dim });
        foreach (var (label, exes) in RuleText.AppGroups(list))
        {
            var x = RoutineDialogs.Btn("Remover");
            x.Click += (_, _) =>
            {
                foreach (var e in exes) { list.Apps.Remove(e); list.AppLabels.Remove(e); }
                Changed();
            };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = label } } };
            if (exes.Count > 1 || exes[0] != label) text.Children.Add(new TextBlock { Text = string.Join(", ", exes), Foreground = Dim, FontSize = 12 });
            var row = new DockPanel();
            row.Children.Add(RoutineDialogs.Docked(x, Dock.Right));
            row.Children.Add(text);
            _apps.Children.Add(row);
        }
    }

    static void Fill(StackPanel panel, List<string> items, Action<string> remove)
    {
        panel.Children.Clear();
        if (items.Count == 0) panel.Children.Add(new TextBlock { Text = "Nenhum.", Foreground = Dim });
        foreach (var item in items.ToList())
        {
            var x = RoutineDialogs.Btn("Remover");
            var i = item;
            x.Click += (_, _) => remove(i);
            var row = new DockPanel();
            row.Children.Add(RoutineDialogs.Docked(x, Dock.Right));
            row.Children.Add(new TextBlock { Text = item, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }
    }

    async void NewTask()
    {
        if (TopLevel.GetTopLevel(this) is not Window w) return;
        var title = new TextBox { Watermark = "Tarefa", MinWidth = 300 };
        var onlyToday = new RadioButton { Content = "Só hoje", IsChecked = true, GroupName = "kind" };
        var scheduled = new RadioButton { Content = "Agendada", GroupName = "kind" };
        var rule = new RuleEditor(DateRule.Weekdays(), null) { IsVisible = false };
        scheduled.IsCheckedChanged += (_, _) => rule.IsVisible = scheduled.IsChecked == true;
        var save = RoutineDialogs.Btn("Salvar", true); var cancel = RoutineDialogs.Btn("Cancelar");
        var error = new TextBlock { IsVisible = false };
        var win = RoutineDialogs.Shell("Nova tarefa", 420, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 10,
            Children = { new TextBlock { Text = "Tarefa" }, title, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { onlyToday, scheduled } }, rule, error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } } }
        });
        TaskItem? made = null;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { error.Text = "Escreva a tarefa"; error.IsVisible = true; return; }
            if (scheduled.IsChecked == true)
            {
                if (rule.Rule is not { } r) { error.Text = "Escolha os dias ou uma data"; error.IsVisible = true; return; }
                made = new TaskItem { Title = title.Text.Trim(), Rule = r };
            }
            else made = new TaskItem { Title = title.Text.Trim(), TempDate = Today };
            win.Close();
        };
        cancel.Click += (_, _) => win.Close();
        await win.ShowDialog(w);
        if (made == null) return;
        Data.Tasks.Add(made);
        Changed();
    }
}

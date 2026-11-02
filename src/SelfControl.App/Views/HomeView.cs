using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Início: a vertical page of blocks (text, picture, today's routine, next events, today's tasks, lock status).</summary>
public class HomeView : UserControl
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    static readonly IBrush Card = new SolidColorBrush(Color.Parse("#0D0D0D"));
    readonly AppState? _state;
    readonly AppData _fallback = new();
    readonly StackPanel _panel = new() { Spacing = 12, Margin = new Thickness(24) };
    string? _notice;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };

    public HomeView() : this(null) { }

    public HomeView(AppState? state)
    {
        _state = state;
        Content = new ScrollViewer { Content = _panel };
        Rebuild();
        if (state != null)
        {
            state.ViewsChanged += Rebuild;
            state.LockLifted += Rebuild;
        }
        _timer.Tick += (_, _) => Rebuild();
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    AppData Data => _state?.Data ?? _fallback;
    DateTime Now => _state?.Now ?? DateTime.Now;
    internal StackPanel Panel => _panel;
    string ImagesFolder => _state?.ImagesFolder ?? Path.Combine(Path.GetTempPath(), "SelfControl-images");

    void Changed() { _state?.DataChanged(); Rebuild(); }

    internal void AddBlock(HomeBlockKind kind)
    {
        Data.Home.Add(new HomeBlock { Kind = kind, Text = kind == HomeBlockKind.Text ? "" : null });
        Changed();
    }

    /// <summary>Moves the block at <paramref name="index"/> by <paramref name="delta"/> places (no-op past either end).</summary>
    internal void Move(int index, int delta)
    {
        var to = index + delta;
        if (index < 0 || index >= Data.Home.Count || to < 0 || to >= Data.Home.Count) return;
        var b = Data.Home[index];
        Data.Home.RemoveAt(index);
        Data.Home.Insert(to, b);
        Changed();
    }

    async void AddImage()
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolha a imagem", AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Imagens") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } } },
        });
        if (files.Count == 0) return;
        try
        {
            var folder = ImagesFolder;
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, Guid.NewGuid().ToString("N") + Path.GetExtension(files[0].Path.LocalPath).ToLowerInvariant());
            File.Copy(files[0].Path.LocalPath, dest);
            Data.Home.Add(new HomeBlock { Kind = HomeBlockKind.Image, ImagePath = Path.GetFileName(dest) });
            _notice = null;
            Changed();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _notice = "Não foi possível copiar a imagem"; Rebuild(); }
    }

    Button AddButton()
    {
        var b = RoutineDialogs.Btn("＋ Bloco", true);
        var menu = new ContextMenu();
        void Item(string text, Action a) { var m = new MenuItem { Header = text }; m.Click += (_, _) => a(); menu.Items.Add(m); }
        Item("Texto", () => AddBlock(HomeBlockKind.Text));
        Item("Imagem", AddImage);
        Item("Rotina de hoje", () => AddBlock(HomeBlockKind.TodayRoutine));
        Item("Próximos eventos", () => AddBlock(HomeBlockKind.NextEvents));
        Item("Tarefas de hoje", () => AddBlock(HomeBlockKind.TodayTasks));
        Item("Status do bloqueio", () => AddBlock(HomeBlockKind.LockStatus));
        b.Click += (_, _) => menu.Open(b);
        return b;
    }

    void Rebuild()
    {
        // Don't yank a text box out from under the typing person (the periodic refresh).
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox tb && _panel.IsVisualAncestorOf(tb)) return;
        _panel.Children.Clear();
        _panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children =
            { new TextBlock { Text = "Início", FontSize = 24, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, AddButton(),
              new TextBlock { Text = _notice, IsVisible = _notice != null, VerticalAlignment = VerticalAlignment.Center } } });
        var blocks = Data.Home.ToList();
        for (int i = 0; i < blocks.Count; i++) _panel.Children.Add(Wrap(blocks[i], i, blocks.Count));
    }

    Control Wrap(HomeBlock block, int index, int count)
    {
        var up = RoutineDialogs.Btn("↑"); up.IsEnabled = index > 0;
        var down = RoutineDialogs.Btn("↓"); down.IsEnabled = index < count - 1;
        up.Click += (_, _) => Move(index, -1);
        down.Click += (_, _) => Move(index, 1);
        var more = RoutineDialogs.Btn("⋯");
        var menu = new ContextMenu();
        var del = new MenuItem { Header = "Excluir" };
        del.Click += (_, _) => { Data.Home.Remove(block); Changed(); };
        menu.Items.Add(del);
        more.Click += (_, _) => menu.Open(more);
        var tools = new StackPanel { Spacing = 2, Margin = new Thickness(8, 0, 0, 0), Children = { up, down, more } };
        var dock = new DockPanel();
        dock.Children.Add(RoutineDialogs.Docked(tools, Dock.Right));
        dock.Children.Add(Body(block));
        return new Border { Background = Card, CornerRadius = new CornerRadius(6), Padding = new Thickness(14), Child = dock };
    }

    Control Body(HomeBlock block) => block.Kind switch
    {
        HomeBlockKind.Text => TextBody(block),
        HomeBlockKind.Image => ImageBody(block),
        HomeBlockKind.TodayRoutine => RoutineBody(),
        HomeBlockKind.NextEvents => EventsBody(),
        HomeBlockKind.TodayTasks => TasksBody(),
        _ => LockBody(),
    };

    Control TextBody(HomeBlock block)
    {
        var box = new TextBox { Text = block.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Watermark = "Escreva aqui…", Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        box.TextChanged += (_, _) => { block.Text = box.Text; _state?.Save(); };
        return box;
    }

    static StackPanel Section(string title) =>
        new() { Spacing = 4, Children = { new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold } } };

    Control ImageBody(HomeBlock block)
    {
        try
        {
            var path = block.ImagePath == null ? null : Path.IsPathRooted(block.ImagePath) ? block.ImagePath : Path.Combine(ImagesFolder, block.ImagePath);
            if (path != null && File.Exists(path))
                return new Image { Source = new Bitmap(path), MaxHeight = 320, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception) { }
        return new TextBlock { Text = "Imagem não encontrada", Foreground = Dim };
    }

    static string Hm(int min) => $"{min / 60:00}:{min % 60:00}";

    Control RoutineBody()
    {
        var s = Section("Rotina de hoje");
        var now = Now;
        var nowMin = now.Hour * 60 + now.Minute;
        var blocks = RoutineResolver.For(Data, DateOnly.FromDateTime(now))?.Blocks
            .Where(b => b.Day == now.DayOfWeek).OrderBy(b => b.StartMin).ToList();
        if (blocks == null || blocks.Count == 0) { s.Children.Add(new TextBlock { Text = "Nada na rotina hoje.", Foreground = Dim }); return s; }
        foreach (var b in blocks)
        {
            var current = nowMin >= b.StartMin && nowMin < b.EndMin;
            s.Children.Add(new Border
            {
                Background = current ? Brushes.White : Brushes.Transparent, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(4),
                Child = new TextBlock { Text = $"{Hm(b.StartMin)}–{Hm(b.EndMin)}  {b.Title}", Foreground = current ? Brushes.Black : null },
            });
        }
        return s;
    }

    Control EventsBody()
    {
        var s = Section("Próximos eventos");
        var now = Now;
        var today = DateOnly.FromDateTime(now);
        var next = Data.Events.Where(e => e.Enabled)
            .Select(e => (e, at: EventScheduler.NextOccurrence(e, now)))
            .Where(x => x.at != null).OrderBy(x => x.at).Take(3).ToList();
        if (next.Count == 0) s.Children.Add(new TextBlock { Text = "Nenhum evento.", Foreground = Dim });
        foreach (var (e, at) in next)
        {
            var d = DateOnly.FromDateTime(at!.Value);
            var day = d == today ? "Hoje" : d == today.AddDays(1) ? "Amanhã" : d.ToString("dd/MM");
            s.Children.Add(new TextBlock { Text = $"{day} {at:HH:mm}  {e.Message}" });
        }
        return s;
    }

    Control TasksBody()
    {
        var s = Section("Tarefas de hoje");
        var today = DateOnly.FromDateTime(Now);
        var tasks = TaskRules.Today(Data, today);
        if (tasks.Count == 0) s.Children.Add(new TextBlock { Text = "Nenhuma tarefa para hoje.", Foreground = Dim });
        foreach (var t in tasks)
        {
            var task = t;
            var box = new CheckBox { Content = task.Title, IsChecked = TaskRules.IsDone(task, today) };
            box.IsCheckedChanged += (_, _) => { TaskRules.SetDone(task, today, box.IsChecked == true); Changed(); };
            s.Children.Add(box);
        }
        return s;
    }

    Control LockBody()
    {
        var s = Section("Bloqueio");
        var today = DateOnly.FromDateTime(Now);
        var pending = TaskRules.Today(Data, today).Count(t => !TaskRules.IsDone(t, today));
        s.Children.Add(new TextBlock { Text = pending > 0 ? $"Bloqueado — {pending} tarefas pendentes" : "Liberado" });
        if (_state != null)
            foreach (var key in Data.BlockList.Apps.Concat(Data.BlockList.Sites))
                if (_state.Tracker.Remaining(key) is { } left)
                    s.Children.Add(new TextBlock { Text = $"{key} — breaktime, {Math.Ceiling(left.TotalMinutes):0} min restantes", Foreground = Dim });
        return s;
    }
}

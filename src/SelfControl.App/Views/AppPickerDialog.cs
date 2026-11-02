using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Pick an installed program, shown like Windows' "Installed apps": icon, name, publisher, with a search box.</summary>
static class AppPickerDialog
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));

    /// <summary>Case-insensitive match on name or publisher.</summary>
    internal static IEnumerable<InstalledApp> Filter(IEnumerable<InstalledApp> apps, string? query)
    {
        var q = (query ?? "").Trim();
        return q.Length == 0 ? apps : apps.Where(a =>
            a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
            (a.Publisher?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false));
    }

    public static async Task<InstalledApp?> Show(Window owner, IAppCatalog catalog)
    {
        InstalledApp? result = null;
        IReadOnlyList<InstalledApp> all = Array.Empty<InstalledApp>();
        var search = new TextBox { Watermark = L.T("Search programs") };
        var list = new ListBox { Background = Brushes.Transparent, ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<InstalledApp?>((a, _) => Row(a)) };
        var status = new TextBlock { Text = L.T("Loading installed programs…"), Foreground = Dim };
        var add = RoutineDialogs.Btn(L.T("Add"), true); var cancel = RoutineDialogs.Btn(L.T("Cancel"));
        add.IsEnabled = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0), Children = { cancel, add } };
        var root = new DockPanel { Margin = new Thickness(16) };
        root.Children.Add(RoutineDialogs.Docked(search, Dock.Top));
        root.Children.Add(RoutineDialogs.Docked(status, Dock.Top));
        root.Children.Add(RoutineDialogs.Docked(buttons, Dock.Bottom));
        root.Children.Add(list);
        var win = RoutineDialogs.Shell(L.T("Choose program"), 520, 600, root);

        void Refill() => list.ItemsSource = Filter(all, search.Text).ToList();
        search.TextChanged += (_, _) => Refill();
        list.SelectionChanged += (_, _) => add.IsEnabled = list.SelectedItem != null;
        list.DoubleTapped += (_, _) => { if (list.SelectedItem is InstalledApp a) { result = a; win.Close(); } };
        add.Click += (_, _) => { result = list.SelectedItem as InstalledApp; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += async (_, _) =>
        {
            search.Focus();
            try { all = await Task.Run(catalog.Installed); } catch { all = Array.Empty<InstalledApp>(); }
            status.Text = all.Count == 0 ? L.T("No programs found. Use \"Browse .exe…\".") : L.F("{0} programs", all.Count);
            Refill();
        };
        await win.ShowDialog(owner);
        return result;
    }

    /// <summary>
    /// One list row. Avalonia calls the template with null while it recycles rows during scrolling; that used to throw
    /// on every scroll step and made the list freeze.
    /// </summary>
    internal static Control Row(InstalledApp? a)
    {
        if (a == null) return new Panel();
        var icon = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0) };
        if (a.IconPng != null) { try { icon.Source = new Bitmap(a.IconPng); } catch { } }
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = a.Name } } };
        if (!string.IsNullOrWhiteSpace(a.Publisher)) text.Children.Add(new TextBlock { Text = a.Publisher, Foreground = Dim, FontSize = 12 });
        return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 4), Children = { icon, text } };
    }
}

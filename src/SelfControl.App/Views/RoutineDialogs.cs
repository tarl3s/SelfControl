using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Small modal dialogs of the Rotina screen, built in code with the app's black/white look.</summary>
static class RoutineDialogs
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));

    static Window Shell(string title, double w, double h, Control content) => new()
    {
        Title = title, Width = w, SizeToContent = h <= 0 ? SizeToContent.Height : SizeToContent.Manual, Height = h <= 0 ? double.NaN : h,
        CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = new SolidColorBrush(Color.Parse("#0D0D0D")), Content = content,
    };

    static Button Btn(string text, bool primary = false)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 6) };
        if (primary) b.Classes.Add("primary");
        return b;
    }

    static T Docked<T>(T c, Dock d) where T : Control { DockPanel.SetDock(c, d); return c; }

    public static async Task<string?> Prompt(Window owner, string title, string label, string initial)
    {
        string? result = null;
        var box = new TextBox { Text = initial, MinWidth = 260 };
        var ok = Btn("OK", true); var cancel = Btn("Cancelar");
        var win = Shell(title, 340, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 10,
            Children = { new TextBlock { Text = label }, box, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, ok } } }
        });
        ok.Click += (_, _) => { result = box.Text?.Trim(); win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        await win.ShowDialog(owner);
        return string.IsNullOrEmpty(result) ? null : result;
    }

    static readonly string?[] Presets = { null, "#3B4A5A", "#4A3B5A", "#3B5A4A", "#5A4A3B", "#5A3B3B", "#4A4A4A" };

    /// <summary>Edit a block: title, colour, Excluir. Returns true when something changed (or the block was deleted).</summary>
    public static async Task<bool> EditBlock(Window owner, Routine routine, RoutineBlock block)
    {
        bool changed = false;
        var title = new TextBox { Text = block.Title, MinWidth = 260 };
        string? color = block.Color;
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void Paint()
        {
            swatches.Children.Clear();
            foreach (var c in Presets)
            {
                var sw = new Button
                {
                    Width = 30, Height = 30, Padding = new Thickness(0), Content = c == null ? "∅" : "",
                    Background = c == null ? Brushes.Black : new SolidColorBrush(Color.Parse(c)),
                    BorderBrush = (c ?? "") == (color ?? "") ? Brushes.White : new SolidColorBrush(Color.Parse("#3A3A3A")),
                    BorderThickness = new Thickness(2),
                };
                ToolTip.SetTip(sw, c == null ? "Sem cor" : c);
                sw.Click += (_, _) => { color = c; Paint(); };
                swatches.Children.Add(sw);
            }
        }
        Paint();
        var save = Btn("Salvar", true); var del = Btn("Excluir"); var cancel = Btn("Cancelar");
        var win = Shell("Editar atividade", 360, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Título" }, title,
                new TextBlock { Text = "Cor" }, swatches,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { del, new Border { Width = 40 }, cancel, save } },
            }
        });
        save.Click += (_, _) => { block.Title = string.IsNullOrWhiteSpace(title.Text) ? "Nova atividade" : title.Text.Trim(); block.Color = color; changed = true; win.Close(); };
        del.Click += (_, _) => { routine.Blocks.Remove(block); changed = true; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
        return changed;
    }

    /// <summary>"Agendar semanas…": which routine applies to which week (Monday). Edits the data directly and calls <paramref name="changed"/>.</summary>
    public static async Task Schedule(Window owner, AppData data, Action changed)
    {
        var list = new StackPanel { Spacing = 4 };
        var date = new CalendarDatePicker { SelectedDate = DateTime.Today.AddDays(7) };
        var picker = new ComboBox { MinWidth = 160, DisplayMemberBinding = new Avalonia.Data.Binding("Name"), ItemsSource = data.Routines, SelectedIndex = data.Routines.Count > 0 ? 0 : -1 };
        var add = Btn("Adicionar", true); var close = Btn("Fechar");
        void Rebuild()
        {
            list.Children.Clear();
            foreach (var s in data.Schedules.OrderBy(x => x.WeekStart).ToList())
            {
                var name = data.Routines.FirstOrDefault(r => r.Id == s.RoutineId)?.Name ?? "?";
                var rm = Btn("Remover");
                var sc = s;
                rm.Click += (_, _) => { data.Schedules.Remove(sc); changed(); Rebuild(); };
                var row = new DockPanel { LastChildFill = true };
                row.Children.Add(Docked(rm, Dock.Right));
                row.Children.Add(new TextBlock { Text = $"Semana de {s.WeekStart:dd/MM/yyyy}  →  {name}", VerticalAlignment = VerticalAlignment.Center });
                list.Children.Add(row);
            }
            if (list.Children.Count == 0)
                list.Children.Add(new TextBlock { Text = "Nenhuma semana agendada.", Foreground = Dim });
        }
        Rebuild();
        add.Click += (_, _) =>
        {
            if (date.SelectedDate is not { } d || picker.SelectedItem is not Routine r) return;
            var wk = RoutineResolver.WeekStart(DateOnly.FromDateTime(d));
            data.Schedules.RemoveAll(s => s.WeekStart == wk);
            data.Schedules.Add(new RoutineSchedule { WeekStart = wk, RoutineId = r.Id });
            changed(); Rebuild();
        };
        var win = Shell("Agendar semanas", 520, 420, new DockPanel
        {
            Margin = new Thickness(16), LastChildFill = true,
            Children =
            {
                Docked(new TextBlock { Text = "A semana (começa na segunda) usa a rotina escolhida.", Foreground = Dim, Margin = new Thickness(0, 0, 0, 8) }, Dock.Top),
                Docked(close, Dock.Bottom),
                Docked(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 8), Children = { date, picker, add } }, Dock.Bottom),
                new ScrollViewer { Content = list },
            }
        });
        close.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
    }
}

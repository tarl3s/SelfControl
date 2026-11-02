using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace SelfControl.App.Views;

/// <summary>Configurações: export / import of all data and the "start with Windows" switch.</summary>
static class SettingsDialog
{
    static readonly FilePickerFileType Json = new(L.T("SelfControl data")) { Patterns = new[] { "*.json" } };

    public static async Task Show(Window owner, AppState state)
    {
        var message = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap };
        void Say(string text) { message.Text = text; message.IsVisible = true; }

        var export = RoutineDialogs.Btn(L.T("Export data…"));
        var import = RoutineDialogs.Btn(L.T("Import data…"));
        var close = RoutineDialogs.Btn(L.T("Close"), true);
        var auto = new CheckBox { Content = L.T("Start with Windows") };
        var breaks = new CheckBox { Content = L.T("Suggest breaks (stretching and eyes) every hour"), IsChecked = state.Data.BreakReminders };
        breaks.IsCheckedChanged += (_, _) => { state.Data.BreakReminders = breaks.IsChecked == true; state.DataChanged(); };
        // Language: null = follow Windows. Applies on restart (every window is built in the language it opened with).
        string?[] langs = { null, L.English, L.Portuguese };
        var language = new ComboBox { MinWidth = 160, SelectedIndex = Math.Max(0, Array.IndexOf(langs, state.Data.Language)) };
        language.Items.Add(L.T("Automatic")); language.Items.Add("English"); language.Items.Add("Português");
        language.SelectionChanged += (_, _) =>
        {
            var chosen = langs[Math.Max(0, language.SelectedIndex)];
            if (chosen == state.Data.Language) return;
            state.Data.Language = chosen; state.DataChanged();
            var lang = chosen ?? L.FromCulture();
            Say(L.In(lang, "Restart SelfControl to change the language"));
        };
        try { auto.IsChecked = state.Autostart.Enabled; } catch (Exception) { auto.IsEnabled = false; }
        bool syncing = false;
        auto.IsCheckedChanged += (_, _) =>
        {
            if (syncing) return;
            try { state.Autostart.Enabled = auto.IsChecked == true; }
            catch (Exception)
            {
                Say(L.T("Could not change starting with Windows"));
                syncing = true;
                try { auto.IsChecked = state.Autostart.Enabled; } catch (Exception) { auto.IsChecked = !auto.IsChecked; }
                syncing = false;
            }
        };

        Window? shell = null;
        var check = RoutineDialogs.Btn(L.T("Check for updates"));
        check.IsEnabled = state.Updater != null;
        check.Click += async (_, _) =>
        {
            if (state.Updater is not { } updater) return;
            check.IsEnabled = false;
            try { if (!await UpdateDialog.CheckNow(shell!, updater)) Say(L.T("You're on the latest version")); }
            finally { check.IsEnabled = true; }
        };
        var win = shell = RoutineDialogs.Shell(L.T("Settings"), 420, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = L.T("Settings"), FontSize = 20, FontWeight = FontWeight.SemiBold },
                auto,
                breaks,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = "Idioma / Language", VerticalAlignment = VerticalAlignment.Center }, language } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { export, import } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = L.F("Version {0}", state.Updater?.CurrentVersion ?? "-"), VerticalAlignment = VerticalAlignment.Center }, check } },
                message,
                new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { close } } }
        });

        export.Click += async (_, _) =>
        {
            var file = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = L.T("Export data"), SuggestedFileName = "selfcontrol-dados.json", DefaultExtension = "json", FileTypeChoices = new[] { Json },
            });
            if (file == null) return;
            try { state.Export(file.Path.LocalPath); Say(L.T("Data exported")); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Say(L.T("Could not save the file")); }
        };
        import.Click += async (_, _) =>
        {
            var files = await win.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = L.T("Import data"), AllowMultiple = false, FileTypeFilter = new[] { Json },
            });
            if (files.Count == 0) return;
            if (!await Confirm(win, L.T("This will replace all of your data. Continue?"))) return;
            Say(state.Import(files[0].Path.LocalPath) ?? L.T("Data imported"));
        };
        close.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
    }

    static async Task<bool> Confirm(Window owner, string text)
    {
        bool ok = false;
        var yes = RoutineDialogs.Btn(L.T("Continue"), true); var no = RoutineDialogs.Btn(L.T("Cancel"));
        var win = RoutineDialogs.Shell(L.T("Confirm"), 360, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } } }
        });
        yes.Click += (_, _) => { ok = true; win.Close(); };
        no.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
        return ok;
    }
}

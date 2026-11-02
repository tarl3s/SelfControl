using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace SelfControl.App.Views;

/// <summary>Configurações: export / import of all data and the "start with Windows" switch.</summary>
static class SettingsDialog
{
    static readonly FilePickerFileType Json = new("Dados do SelfControl") { Patterns = new[] { "*.json" } };

    public static async Task Show(Window owner, AppState state)
    {
        var message = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap };
        void Say(string text) { message.Text = text; message.IsVisible = true; }

        var export = RoutineDialogs.Btn("Exportar dados…");
        var import = RoutineDialogs.Btn("Importar dados…");
        var close = RoutineDialogs.Btn("Fechar", true);
        var auto = new CheckBox { Content = "Iniciar com o Windows" };
        var breaks = new CheckBox { Content = "Sugerir pausas (alongamento e olhos) a cada hora", IsChecked = state.Data.BreakReminders };
        breaks.IsCheckedChanged += (_, _) => { state.Data.BreakReminders = breaks.IsChecked == true; state.DataChanged(); };
        try { auto.IsChecked = state.Autostart.Enabled; } catch (Exception) { auto.IsEnabled = false; }
        bool syncing = false;
        auto.IsCheckedChanged += (_, _) =>
        {
            if (syncing) return;
            try { state.Autostart.Enabled = auto.IsChecked == true; }
            catch (Exception)
            {
                Say("Não foi possível alterar a inicialização com o Windows");
                syncing = true;
                try { auto.IsChecked = state.Autostart.Enabled; } catch (Exception) { auto.IsChecked = !auto.IsChecked; }
                syncing = false;
            }
        };

        var win = RoutineDialogs.Shell("Configurações", 420, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = "Configurações", FontSize = 20, FontWeight = FontWeight.SemiBold },
                auto,
                breaks,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { export, import } },
                message,
                new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { close } } }
        });

        export.Click += async (_, _) =>
        {
            var file = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar dados", SuggestedFileName = "selfcontrol-dados.json", DefaultExtension = "json", FileTypeChoices = new[] { Json },
            });
            if (file == null) return;
            try { state.Export(file.Path.LocalPath); Say("Dados exportados"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Say("Não foi possível salvar o arquivo"); }
        };
        import.Click += async (_, _) =>
        {
            var files = await win.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar dados", AllowMultiple = false, FileTypeFilter = new[] { Json },
            });
            if (files.Count == 0) return;
            if (!await Confirm(win, "Isso vai substituir todos os seus dados. Continuar?")) return;
            Say(state.Import(files[0].Path.LocalPath) ?? "Dados importados");
        };
        close.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
    }

    static async Task<bool> Confirm(Window owner, string text)
    {
        bool ok = false;
        var yes = RoutineDialogs.Btn("Continuar", true); var no = RoutineDialogs.Btn("Cancelar");
        var win = RoutineDialogs.Shell("Confirmar", 360, 0, new StackPanel
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

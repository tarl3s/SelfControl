using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>"A new version is available": release notes, Update now / Later.</summary>
static class UpdateDialog
{
    /// <summary>The version the person said "Later" to (until the next start).</summary>
    static string? _postponed;

    public static bool IsPostponed(UpdateInfo u) => _postponed == u.Version;

    public static async Task Show(Window owner, IUpdater updater, UpdateInfo update)
    {
        var status = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap };
        var notes = new TextBox { Text = update.Notes, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 180, AcceptsReturn = true };
        var now = RoutineDialogs.Btn(L.T("Update now"), true);
        var later = RoutineDialogs.Btn(L.T("Later"));
        var win = RoutineDialogs.Shell(L.T("Update"), 460, 0, new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = L.F("A new version of SelfControl is available: v{0}", update.Version), FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                notes, status,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { later, now } } }
        });
        later.Click += (_, _) => { _postponed = update.Version; win.Close(); };
        now.Click += async (_, _) =>
        {
            now.IsEnabled = later.IsEnabled = false;
            status.IsVisible = true; status.Text = L.T("Downloading...");
            var progress = new Progress<double>(p => status.Text = L.F("Downloading... {0}%", (int)(p * 100)));
            try
            {
                await updater.InstallAsync(update, progress);
                status.Text = L.T("Restarting...");
                App.QuitApp();
            }
            catch (Exception)
            {
                status.Text = L.T("The update failed. Try again later.");
                later.IsEnabled = true;
            }
        };
        await win.ShowDialog(owner);
    }

    /// <summary>True (and the dialog shown) when a newer version exists.</summary>
    public static async Task<bool> CheckNow(Window owner, IUpdater updater)
    {
        var u = await updater.CheckAsync();
        if (u == null) return false;
        await Show(owner, updater, u);
        return true;
    }
}

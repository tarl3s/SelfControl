using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SelfControl.Core;

namespace SelfControl.Desktop;

/// <summary>
/// Programs from the same registry keys Windows' "Installed apps" page reads (HKLM 64/32-bit and HKCU Uninstall).
/// Icons are extracted once into a PNG cache so the portable UI can show them.
/// </summary>
public sealed class WindowsAppCatalog : IAppCatalog
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    readonly string _iconFolder;

    public WindowsAppCatalog(string iconFolder) => _iconFolder = iconFolder;

    public IReadOnlyList<InstalledApp> Installed()
    {
        var byName = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(UninstallKey);
                if (key == null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var k = key.OpenSubKey(sub);
                        if (k == null || Read(k).app is not { } app) continue;
                        if (!byName.ContainsKey(app.Name)) byName[app.Name] = app;
                    }
                    catch { /* one broken entry must not hide the others */ }
                }
            }
            catch { }
        }
        return byName.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    (InstalledApp? app, int _) Read(RegistryKey k)
    {
        var name = k.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(name)) return (null, 0);
        // Same filters as "Installed apps": hide system components and updates/patches.
        if (k.GetValue("SystemComponent") is int sc && sc == 1) return (null, 0);
        if (k.GetValue("ParentKeyName") != null || k.GetValue("ReleaseType") is string rt && rt.Contains("Update")) return (null, 0);
        var icon = k.GetValue("DisplayIcon") as string;
        if (InWindows(AppCatalogRules.IconFile(icon))) icon = null;
        // Where the program lives: InstallLocation, else the folder of its icon, else of its uninstaller (Discord's Update.exe).
        var folder = new[] { (k.GetValue("InstallLocation") as string)?.Trim().Trim('"'), Dir(AppCatalogRules.IconFile(icon)), Dir(UninstallExe(k.GetValue("UninstallString") as string)) }
            .FirstOrDefault(f => !string.IsNullOrEmpty(f) && !InWindows(f) && Directory.Exists(f));
        var found = FolderExes(folder).ToList();
        if (found.Count > 40) found.Clear();   // a shared folder (e.g. Program Files itself), not one program
        var exes = AppCatalogRules.PickExes(icon, found);
        if (exes.Count == 0) return (null, 0);
        return (new InstalledApp(name.Trim(), k.GetValue("Publisher") as string, exes, IconPng(icon, folder, exes[0])), 0);
    }

    static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
    static bool InWindows(string? path) => path != null && path.StartsWith(WinDir, StringComparison.OrdinalIgnoreCase);
    static string? Dir(string? file) { try { return file == null ? null : Path.GetDirectoryName(file); } catch { return null; } }

    /// <summary>The exe in an UninstallString: "\"C:\\a\\Update.exe\" --uninstall" or "C:\\a\\unins000.exe /x".</summary>
    static string? UninstallExe(string? cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return null;
        var s = cmd.Trim();
        if (s.StartsWith('"')) { var end = s.IndexOf('"', 1); return end > 1 ? s[1..end] : null; }
        var i = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? s[..(i + 4)] : null;
    }

    /// <summary>Exes in the install folder and one level below (e.g. Discord's app-1.0.x\Discord.exe).</summary>
    static IEnumerable<string> FolderExes(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) yield break;
        string[] top, subs;
        try { top = Directory.GetFiles(folder, "*.exe"); subs = Directory.GetDirectories(folder); } catch { yield break; }
        foreach (var f in top) yield return f;
        foreach (var d in subs.Take(30))
        {
            string[] files;
            try { files = Directory.GetFiles(d, "*.exe"); } catch { continue; }
            foreach (var f in files.Take(20)) yield return f;
        }
    }

    string? IconPng(string? displayIcon, string? folder, string exe)
    {
        var source = AppCatalogRules.IconFile(displayIcon);
        if (source == null || !File.Exists(source))
            source = folder == null ? null : Directory.EnumerateFiles(folder, exe, SearchOption.AllDirectories).FirstOrDefault();
        if (source == null || !File.Exists(source)) return null;
        try
        {
            var png = Path.Combine(_iconFolder, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(source.ToLowerInvariant())))[..16] + ".png");
            if (File.Exists(png)) return png;
            Directory.CreateDirectory(_iconFolder);
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(source);
            if (ico == null) return null;
            using var bmp = ico.ToBitmap();
            bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            return png;
        }
        catch { return null; }
    }
}

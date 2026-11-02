namespace SelfControl.Core;

/// <summary>Turns an "Installed apps" registry entry into the executables to block.</summary>
public static class AppCatalogRules
{
    static readonly string[] HelperWords = { "unins", "uninst", "setup", "install", "update", "updater", "crash", "report", "helper", "elevat", "repair", "redist", "vcredist", "dxsetup" };

    /// <summary>Uninstallers, updaters, crash reporters… are not the program itself.</summary>
    public static bool IsHelperExe(string exeName)
    {
        var n = Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant();
        return HelperWords.Any(n.Contains);
    }

    /// <summary>The file path inside a DisplayIcon value: "\"C:\\a\\b.exe\",0" → "C:\\a\\b.exe".</summary>
    public static string? IconFile(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return null;
        var s = displayIcon.Trim();
        var comma = s.LastIndexOf(',');
        if (comma > 0 && int.TryParse(s[(comma + 1)..].Trim(), out _)) s = s[..comma];
        s = s.Trim().Trim('"').Trim();
        return s.Length == 0 ? null : s;
    }

    /// <summary>
    /// Exe names to block: the DisplayIcon exe (unless it's a helper) plus the non-helper exes found in the install
    /// folder (passed in as full paths), lowercase, distinct, never a protected exe.
    /// </summary>
    public static List<string> PickExes(string? displayIcon, IEnumerable<string> folderExes)
    {
        var result = new List<string>();
        void Add(string path)
        {
            var exe = BlockRules.NormalizeApp(path);
            if (exe.Length > 0 && !IsHelperExe(exe) && BlockRules.CanBlockApp(exe) && !result.Contains(exe)) result.Add(exe);
        }
        var icon = IconFile(displayIcon);
        // A browser (or SelfControl itself) is never blocked as a program: sites go through the extension.
        if (icon != null && icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !BlockRules.CanBlockApp(BlockRules.NormalizeApp(icon))) return result;
        if (icon != null && icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Add(icon);
        foreach (var p in folderExes) Add(p);
        return result;
    }
}

namespace SelfControl.Core;

public static class BlockRules
{
    public static readonly IReadOnlySet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "selfcontrol.exe", "explorer.exe", "brave.exe", "chrome.exe", "msedge.exe", "opera.exe", "firefox.exe",
    };

    static string StripWww(string h) => h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h[4..] : h;

    public static bool MatchesSite(string host, string domain)
    {
        var h = StripWww((host ?? "").Trim().TrimEnd('.')).ToLowerInvariant();
        var d = StripWww((domain ?? "").Trim().TrimEnd('.')).ToLowerInvariant();
        if (h.Length == 0 || d.Length == 0) return false;
        return h == d || h.EndsWith("." + d, StringComparison.Ordinal);
    }

    /// <summary>"C:\x\Steam.exe" or "Steam" -> "steam.exe".</summary>
    public static string NormalizeApp(string pathOrName)
    {
        var s = (pathOrName ?? "").Trim().Trim('"');
        var i = s.LastIndexOfAny(new[] { (char)92, (char)47 });
        if (i >= 0) s = s[(i + 1)..];
        s = s.ToLowerInvariant();
        if (s.Length > 0 && !s.EndsWith(".exe", StringComparison.Ordinal)) s += ".exe";
        return s;
    }

    public static bool CanBlockApp(string exe)
    {
        var n = NormalizeApp(exe);
        return n.Length > 0 && !Protected.Contains(n);
    }
}

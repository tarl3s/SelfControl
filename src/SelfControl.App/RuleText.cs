using SelfControl.Core;

namespace SelfControl.App;

/// <summary>Text helpers shared by the Eventos and Tarefas screens (rule summaries, block list input).</summary>
public static class RuleText
{
    static readonly DayOfWeek[] Order =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
    static readonly string[] Names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static string DayName(DayOfWeek d) => L.T(Names[Array.IndexOf(Order, d)]);
    public static IReadOnlyList<DayOfWeek> WeekOrder => Order;

    static bool Same(DateRule r, DateRule other) =>
        r.Days.Distinct().Count() == other.Days.Length && other.Days.All(d => r.Days.Contains(d));

    /// <summary>Only the "when" part: L.T("Mon–Fri"), L.T("Every day"), L.T("Weekend"), "Ter, Qui" or "12/11/2026".</summary>
    public static string Days(DateRule rule)
    {
        if (rule.Date is { } d) return d.ToString("dd/MM/yyyy");
        if (Same(rule, DateRule.Weekdays())) return L.T("Mon–Fri");
        if (Same(rule, DateRule.Weekend())) return L.T("Weekend");
        if (rule.Days.Distinct().Count() == 7) return L.T("Every day");
        if (rule.Days.Length == 0) return "—";
        return string.Join(", ", Order.Where(rule.Days.Contains).Select(DayName));
    }

    public static string Summary(DateRule rule, TimeOnly time) => Days(rule) + " " + time.ToString("HH:mm");

    /// <summary>"https://www.YouTube.com/a/b" becomes "youtube.com".</summary>
    public static string NormalizeSite(string input)
    {
        var s = (input ?? "").Trim();
        var i = s.IndexOf("://", StringComparison.Ordinal);
        if (i >= 0) s = s[(i + 3)..];
        var cut = s.IndexOfAny(new[] { '/', '?', '#' });
        if (cut >= 0) s = s[..cut];
        s = s.ToLowerInvariant().Trim().TrimEnd('.');
        if (s.StartsWith("www.")) s = s[4..];
        return s;
    }

    /// <summary>Adds an app to the list. Returns an error message, or null when added (or already there).</summary>
    public static string? TryAddApp(BlockList list, string pathOrName)
    {
        var exe = BlockRules.NormalizeApp(pathOrName);
        if (exe.Length == 0) return L.T("Choose a program");
        if (!BlockRules.CanBlockApp(exe)) return L.T("This program cannot be blocked");
        if (!list.Apps.Contains(exe)) list.Apps.Add(exe);
        return null;
    }

    /// <summary>Adds every exe of an installed program, remembering its name for the list.</summary>
    public static string? TryAddProgram(BlockList list, InstalledApp app)
    {
        var added = false;
        foreach (var exe in app.Exes.Where(BlockRules.CanBlockApp))
        {
            if (!list.Apps.Contains(exe)) list.Apps.Add(exe);
            list.AppLabels[exe] = app.Name;
            added = true;
        }
        return added ? null : L.T("This program cannot be blocked");
    }

    /// <summary>Blocked apps grouped by program name (an exe without a name is its own group).</summary>
    public static List<(string Label, List<string> Exes)> AppGroups(BlockList list) =>
        list.Apps.GroupBy(e => list.AppLabels.TryGetValue(e, out var n) ? n : e)
            .Select(g => (g.Key, g.ToList())).ToList();

    /// <summary>Adds a site. Returns an error message, or null when added (or already there).</summary>
    public static string? TryAddSite(BlockList list, string input)
    {
        var site = NormalizeSite(input);
        if (site.Length == 0 || !site.Contains('.') || site.Contains(' ')) return L.T("Type an address, like youtube.com");
        if (!list.Sites.Contains(site)) list.Sites.Add(site);
        return null;
    }
}

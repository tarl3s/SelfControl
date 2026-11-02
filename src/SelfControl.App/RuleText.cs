using SelfControl.Core;

namespace SelfControl.App;

/// <summary>Text helpers shared by the Eventos and Tarefas screens (rule summaries, block list input).</summary>
public static class RuleText
{
    static readonly DayOfWeek[] Order =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
    static readonly string[] Names = { "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom" };

    public static string DayName(DayOfWeek d) => Names[Array.IndexOf(Order, d)];
    public static IReadOnlyList<DayOfWeek> WeekOrder => Order;

    static bool Same(DateRule r, DateRule other) =>
        r.Days.Distinct().Count() == other.Days.Length && other.Days.All(d => r.Days.Contains(d));

    /// <summary>Only the "when" part: "Seg–Sex", "Todo dia", "Fim de semana", "Ter, Qui" or "12/11/2026".</summary>
    public static string Days(DateRule rule)
    {
        if (rule.Date is { } d) return d.ToString("dd/MM/yyyy");
        if (Same(rule, DateRule.Weekdays())) return "Seg–Sex";
        if (Same(rule, DateRule.Weekend())) return "Fim de semana";
        if (rule.Days.Distinct().Count() == 7) return "Todo dia";
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
        if (exe.Length == 0) return "Escolha um programa";
        if (!BlockRules.CanBlockApp(exe)) return "Esse programa não pode ser bloqueado";
        if (!list.Apps.Contains(exe)) list.Apps.Add(exe);
        return null;
    }

    /// <summary>Adds a site. Returns an error message, or null when added (or already there).</summary>
    public static string? TryAddSite(BlockList list, string input)
    {
        var site = NormalizeSite(input);
        if (site.Length == 0 || !site.Contains('.') || site.Contains(' ')) return "Digite um endereço, como youtube.com";
        if (!list.Sites.Contains(site)) list.Sites.Add(site);
        return null;
    }
}

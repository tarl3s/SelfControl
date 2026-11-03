namespace SelfControl.Core;

/// <summary>
/// Time of real use per day, per program and per site: only while the screen is unlocked and a key or the mouse was
/// used in the last 15 minutes. Keeps the current week and the 3 before it.
/// </summary>
public static class Usage
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(15);
    public const int Weeks = 4;

    public static bool IsActive(IActivity? a) => a == null || (!a.ScreenLocked && a.IdleFor < IdleLimit);

    static string Key(DateOnly d) => d.ToString("yyyy-MM-dd");

    /// <summary>The Monday of <paramref name="d"/>'s week.</summary>
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static TimeSpan On(AppData data, DateOnly day) =>
        TimeSpan.FromSeconds(data.UsageSeconds.TryGetValue(Key(day), out var s) ? s : 0);

    /// <summary>Adds <paramref name="t"/> of use on <paramref name="day"/>, to the program and site in front (when known).</summary>
    public static void Add(AppData data, DateOnly day, TimeSpan t, string? app = null, string? site = null)
    {
        if (t <= TimeSpan.Zero) return;
        int sec = (int)Math.Round(t.TotalSeconds);
        var k = Key(day);
        data.UsageSeconds[k] = (data.UsageSeconds.TryGetValue(k, out var s) ? s : 0) + sec;
        if (!string.IsNullOrWhiteSpace(app)) Bump(data.UsageApps, k, app.Trim(), sec);
        if (!string.IsNullOrWhiteSpace(site)) Bump(data.UsageSites, k, site.Trim().ToLowerInvariant(), sec);
        Prune(data, day);
    }

    static void Bump(Dictionary<string, Dictionary<string, int>> map, string day, string name, int sec)
    {
        if (!map.TryGetValue(day, out var m)) map[day] = m = new(StringComparer.OrdinalIgnoreCase);
        m[name] = (m.TryGetValue(name, out var s) ? s : 0) + sec;
    }

    /// <summary>Forgets days before the Monday 3 weeks before <paramref name="today"/>'s week.</summary>
    public static void Prune(AppData data, DateOnly today)
    {
        var oldest = Key(WeekStart(today).AddDays(-7 * (Weeks - 1)));
        foreach (var old in data.UsageSeconds.Keys.Where(x => string.CompareOrdinal(x, oldest) < 0).ToList()) data.UsageSeconds.Remove(old);
        foreach (var old in data.UsageApps.Keys.Where(x => string.CompareOrdinal(x, oldest) < 0).ToList()) data.UsageApps.Remove(old);
        foreach (var old in data.UsageSites.Keys.Where(x => string.CompareOrdinal(x, oldest) < 0).ToList()) data.UsageSites.Remove(old);
    }

    /// <summary>Program (or site) totals over the given days, longest first.</summary>
    public static List<(string Name, TimeSpan Time)> Top(Dictionary<string, Dictionary<string, int>> map, IEnumerable<DateOnly> days)
    {
        var sum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in days)
            if (map.TryGetValue(Key(d), out var m))
                foreach (var (name, s) in m) sum[name] = (sum.TryGetValue(name, out var x) ? x : 0) + s;
        return sum.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => (p.Key, TimeSpan.FromSeconds(p.Value))).ToList();
    }

    /// <summary>"2h 15min", "40min".</summary>
    public static string Format(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}min" : $"{t.Minutes}min";
}

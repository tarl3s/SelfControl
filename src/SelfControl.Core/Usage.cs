namespace SelfControl.Core;

/// <summary>Time of real use per day: only while the screen is unlocked and a key or the mouse was used in the last 15 minutes.</summary>
public static class Usage
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(15);
    const int KeepDays = 60;

    public static bool IsActive(IActivity? a) => a == null || (!a.ScreenLocked && a.IdleFor < IdleLimit);

    static string Key(DateOnly d) => d.ToString("yyyy-MM-dd");

    public static TimeSpan On(AppData data, DateOnly day) =>
        TimeSpan.FromSeconds(data.UsageSeconds.TryGetValue(Key(day), out var s) ? s : 0);

    public static void Add(AppData data, DateOnly day, TimeSpan t)
    {
        if (t <= TimeSpan.Zero) return;
        var k = Key(day);
        data.UsageSeconds[k] = (data.UsageSeconds.TryGetValue(k, out var s) ? s : 0) + (int)Math.Round(t.TotalSeconds);
        var oldest = Key(day.AddDays(-KeepDays));
        foreach (var old in data.UsageSeconds.Keys.Where(x => string.CompareOrdinal(x, oldest) < 0).ToList())
            data.UsageSeconds.Remove(old);
    }

    /// <summary>"2h 15min", "40min".</summary>
    public static string Format(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}min" : $"{t.Minutes}min";
}

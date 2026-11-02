namespace SelfControl.Core;

public static class EventScheduler
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    /// <summary>Enabled events whose occurrence lies in (lastCheck, now] and is at most 2 minutes old.</summary>
    public static List<ReminderEvent> Due(IEnumerable<ReminderEvent> events, DateTime lastCheck, DateTime now)
    {
        var result = new List<ReminderEvent>();
        if (now <= lastCheck) return result;
        foreach (var ev in events)
        {
            if (!ev.Enabled) continue;
            for (var day = DateOnly.FromDateTime(lastCheck); day <= DateOnly.FromDateTime(now); day = day.AddDays(1))
            {
                if (!ev.Rule.Matches(day)) continue;
                var occ = day.ToDateTime(ev.Time);
                if (occ > lastCheck && occ <= now && now - occ <= Grace) { result.Add(ev); break; }
            }
        }
        return result;
    }

    /// <summary>
    /// Removes one-time events that already happened (once their 2-minute firing window is over). Returns how many.
    /// </summary>
    public static int RemovePast(List<ReminderEvent> events, DateTime now) =>
        events.RemoveAll(ev => ev.Rule.Date is { } d && now - d.ToDateTime(ev.Time) > Grace);

    /// <summary>First occurrence strictly after <paramref name="from"/>, or null if there is none.</summary>
    public static DateTime? NextOccurrence(ReminderEvent ev, DateTime from)
    {
        if (ev.Rule.Date is { } only)
        {
            var occ = only.ToDateTime(ev.Time);
            return occ > from ? occ : null;
        }
        var start = DateOnly.FromDateTime(from);
        for (int i = 0; i <= 7; i++)
        {
            var day = start.AddDays(i);
            if (!ev.Rule.Matches(day)) continue;
            var occ = day.ToDateTime(ev.Time);
            if (occ > from) return occ;
        }
        return null;
    }
}

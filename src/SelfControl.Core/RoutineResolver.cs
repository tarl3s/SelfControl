namespace SelfControl.Core;

public static class RoutineResolver
{
    /// <summary>The Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Week override, then the week's schedule, then the active routine.</summary>
    public static Routine? For(AppData data, DateOnly date)
    {
        var week = WeekStart(date);
        Guid? id = data.WeekOverrides.FirstOrDefault(o => o.WeekStart == week)?.RoutineId
                   ?? data.Schedules.FirstOrDefault(s => s.WeekStart == week)?.RoutineId
                   ?? data.ActiveRoutineId;
        return id is { } g ? data.Routines.FirstOrDefault(r => r.Id == g) : null;
    }

    /// <summary>Manual switch: sets the active routine; in a scheduled week it also overrides that week.</summary>
    public static void SetManual(AppData data, Guid routineId, DateOnly today)
    {
        data.ActiveRoutineId = routineId;
        var week = WeekStart(today);
        if (!data.Schedules.Any(s => s.WeekStart == week)) return;
        data.WeekOverrides.RemoveAll(o => o.WeekStart == week);
        data.WeekOverrides.Add(new WeekOverride { WeekStart = week, RoutineId = routineId });
    }
}

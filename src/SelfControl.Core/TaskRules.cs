namespace SelfControl.Core;

public static class TaskRules
{
    public static IReadOnlyList<TaskItem> Today(AppData data, DateOnly date) =>
        data.Tasks.Where(t => t.TempDate is { } d ? d == date : t.Rule is { Days: not null } r && r.Matches(date)).ToList();

    public static bool IsDone(TaskItem task, DateOnly date) => task.DoneOn.Contains(date);

    public static void SetDone(TaskItem task, DateOnly date, bool done)
    {
        if (done) task.DoneOn.Add(date); else task.DoneOn.Remove(date);
    }

    public static bool IsLocked(AppData data, DateOnly date) =>
        Today(data, date).Any(t => !IsDone(t, date));
}

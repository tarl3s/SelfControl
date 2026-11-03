namespace SelfControl.Core;

public class Routine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<RoutineBlock> Blocks { get; set; } = new();
}

public class RoutineBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DayOfWeek Day { get; set; }
    /// <summary>Minutes since midnight.</summary>
    public int StartMin { get; set; }
    public int EndMin { get; set; }
    public string? Color { get; set; }
    /// <summary>Optional free text shown under the title (e.g. "Legs and arms").</summary>
    public string? Note { get; set; }
}

/// <summary>Which routine applies to the week starting on <see cref="WeekStart"/> (a Monday).</summary>
public class RoutineSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoutineId { get; set; }
    public DateOnly WeekStart { get; set; }
}

public class WeekOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly WeekStart { get; set; }
    public Guid RoutineId { get; set; }
}

public class ReminderEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = "";
    public TimeOnly Time { get; set; }
    public DateRule Rule { get; set; } = DateRule.Weekdays();
    public bool Enabled { get; set; } = true;
}

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateRule? Rule { get; set; }
    public DateOnly? TempDate { get; set; }
    public HashSet<DateOnly> DoneOn { get; set; } = new();
}

public class BlockList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Executable names, lowercase, no path (e.g. "chrome.exe").</summary>
    public List<string> Apps { get; set; } = new();
    /// <summary>Domains (e.g. "youtube.com").</summary>
    public List<string> Sites { get; set; } = new();
    /// <summary>Program name shown for each blocked exe (e.g. "steam.exe" → "Steam"). Optional.</summary>
    public Dictionary<string, string> AppLabels { get; set; } = new();
}

public enum HomeBlockKind { Text, Image, TodayRoutine, NextEvents, TodayTasks, LockStatus }

public class HomeBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public HomeBlockKind Kind { get; set; }
    public string? Text { get; set; }
    public string? ImagePath { get; set; }
}

public class AppData
{
    public int Version { get; set; } = 1;
    public List<Routine> Routines { get; set; } = new();
    public Guid? ActiveRoutineId { get; set; }
    public List<RoutineSchedule> Schedules { get; set; } = new();
    public List<WeekOverride> WeekOverrides { get; set; } = new();
    public List<ReminderEvent> Events { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public BlockList BlockList { get; set; } = new();
    public List<HomeBlock> Home { get; set; } = new();
    /// <summary>Suggest stretches / eye exercises after every hour the computer is in use.</summary>
    public bool BreakReminders { get; set; } = true;
    /// <summary>"en", "pt-BR", or null to follow the Windows language.</summary>
    public string? Language { get; set; }
    /// <summary>Seconds of real use per day ("yyyy-MM-dd"), last 60 days.</summary>
    public Dictionary<string, int> UsageSeconds { get; set; } = new();
}

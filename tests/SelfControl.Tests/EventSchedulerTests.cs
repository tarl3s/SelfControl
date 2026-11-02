using SelfControl.Core;

namespace SelfControl.Tests;

public class EventSchedulerTests
{
    private static ReminderEvent MonFri2130(bool enabled = true) => new()
    {
        Message = "x", Time = new TimeOnly(21, 30), Rule = DateRule.Weekdays(), Enabled = enabled
    };

    [Fact]
    public void MonFri2130_FiresMonday_NotSaturday()
    {
        var ev = MonFri2130();
        // 2026-11-02 Monday, 2026-11-07 Saturday.
        var mon = EventScheduler.Due(new[] { ev }, new DateTime(2026, 11, 2, 21, 29, 0), new DateTime(2026, 11, 2, 21, 30, 30));
        Assert.Single(mon);
        var sat = EventScheduler.Due(new[] { ev }, new DateTime(2026, 11, 7, 21, 29, 0), new DateTime(2026, 11, 7, 21, 30, 30));
        Assert.Empty(sat);
    }

    [Fact]
    public void Missed_After10Min_NotFired()
    {
        var due = EventScheduler.Due(new[] { MonFri2130() }, new DateTime(2026, 11, 2, 21, 0, 0), new DateTime(2026, 11, 2, 21, 40, 0));
        Assert.Empty(due);
    }

    [Fact]
    public void Disabled_NeverDue()
    {
        var due = EventScheduler.Due(new[] { MonFri2130(false) }, new DateTime(2026, 11, 2, 21, 29, 0), new DateTime(2026, 11, 2, 21, 30, 30));
        Assert.Empty(due);
    }

    [Fact]
    public void NextOccurrence_SkipsToMonday()
    {
        var next = EventScheduler.NextOccurrence(MonFri2130(), new DateTime(2026, 11, 7, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 11, 9, 21, 30, 0), next);
    }
}

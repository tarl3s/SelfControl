using SelfControl.Core;

namespace SelfControl.Tests;

public class BreaksTests
{
    static readonly DateTime T0 = new(2026, 11, 2, 9, 0, 0);

    [Fact]
    public void BreakClock_FiresAfterAnHourOfTicks_ThenStartsOver()
    {
        var c = new BreakClock(T0);
        var fired = 0;
        for (int i = 1; i <= 240; i++) if (c.Tick(T0.AddSeconds(30 * i))) fired++;   // 2 hours of 30 s ticks
        Assert.Equal(2, fired);
    }

    [Fact]
    public void BreakClock_LongGap_ResetsTheHour()
    {
        var c = new BreakClock(T0);
        for (int i = 1; i <= 100; i++) c.Tick(T0.AddSeconds(30 * i));          // 50 min of use
        var wake = T0.AddMinutes(50).AddHours(3);                               // PC slept
        Assert.False(c.Tick(wake));
        Assert.False(c.Tick(wake.AddMinutes(30)));                              // > 5 min gap again: still reset
        for (int i = 1; i < 120; i++) Assert.False(c.Tick(wake.AddMinutes(30).AddSeconds(30 * i)));
    }

    [Fact]
    public void RemovePast_DropsOnlyFinishedOneTimeEvents()
    {
        var now = new DateTime(2026, 11, 2, 15, 0, 0);
        var events = new List<ReminderEvent>
        {
            new() { Message = "passou", Time = new TimeOnly(14, 0), Rule = DateRule.On(new DateOnly(2026, 11, 2)) },
            new() { Message = "agora", Time = new TimeOnly(14, 59), Rule = DateRule.On(new DateOnly(2026, 11, 2)) },
            new() { Message = "depois", Time = new TimeOnly(16, 0), Rule = DateRule.On(new DateOnly(2026, 11, 2)) },
            new() { Message = "toda seg", Time = new TimeOnly(8, 0), Rule = DateRule.Every(DayOfWeek.Monday) },
        };
        Assert.Equal(1, EventScheduler.RemovePast(events, now));
        Assert.Equal(new[] { "agora", "depois", "toda seg" }, events.Select(e => e.Message));
    }
}

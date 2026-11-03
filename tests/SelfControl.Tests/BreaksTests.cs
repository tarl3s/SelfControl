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
    public void BreakClock_SleepAndIdle_PauseTheHour()
    {
        var c = new BreakClock(T0);
        for (int i = 1; i <= 100; i++) Assert.False(c.Tick(T0.AddSeconds(30 * i)));   // 50 min of use
        var wake = T0.AddMinutes(50).AddHours(3);                                     // PC slept: not counted
        Assert.False(c.Tick(wake));
        for (int i = 1; i <= 40; i++) Assert.False(c.Tick(wake.AddSeconds(30 * i), active: false));  // idle 20 min
        var back = wake.AddMinutes(20);
        int fired = 0, at = 0;
        for (int i = 1; i <= 40 && fired == 0; i++) if (c.Tick(back.AddSeconds(30 * i))) { fired++; at = i; }
        Assert.Equal(20, at);                                                         // the hour went on from 50 min
    }

    sealed class FakeActivity : IActivity { public TimeSpan IdleFor { get; set; } public bool ScreenLocked { get; set; } }

    [Fact]
    public void Usage_ActiveOnlyWhenUnlockedAndRecentlyUsed()
    {
        Assert.True(Usage.IsActive(new FakeActivity { IdleFor = TimeSpan.FromMinutes(14) }));
        Assert.False(Usage.IsActive(new FakeActivity { IdleFor = TimeSpan.FromMinutes(15) }));
        Assert.False(Usage.IsActive(new FakeActivity { ScreenLocked = true }));
    }

    [Fact]
    public void Usage_AddsPerDay_FormatsAndForgetsOldDays()
    {
        var d = new AppData(); var day = new DateOnly(2026, 11, 3);
        Usage.Add(d, day.AddDays(-61), TimeSpan.FromHours(1));
        Usage.Add(d, day, TimeSpan.FromMinutes(90));
        Usage.Add(d, day, TimeSpan.FromMinutes(45));
        Assert.Equal(TimeSpan.FromMinutes(135), Usage.On(d, day));
        Assert.Equal(TimeSpan.Zero, Usage.On(d, day.AddDays(-61)));
        Assert.Equal("2h 15min", Usage.Format(TimeSpan.FromMinutes(135)));
        Assert.Equal("40min", Usage.Format(TimeSpan.FromMinutes(40)));
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

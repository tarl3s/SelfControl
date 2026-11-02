using SelfControl.Core;

namespace SelfControl.Tests;

public class RoutineResolverTests
{
    // 2026-11-02 is a Monday.
    static readonly DateOnly Mon = new(2026, 11, 2);

    static (AppData d, Routine a, Routine b, Routine c) Setup()
    {
        var a = new Routine { Name = "A" };
        var b = new Routine { Name = "B" };
        var c = new Routine { Name = "C" };
        var d = new AppData { Routines = { a, b, c }, ActiveRoutineId = a.Id };
        return (d, a, b, c);
    }

    [Fact]
    public void Resolver_ScheduledWeekWins_OverActive()
    {
        var (d, _, b, _) = Setup();
        d.Schedules.Add(new RoutineSchedule { RoutineId = b.Id, WeekStart = Mon });
        Assert.Same(b, RoutineResolver.For(d, Mon.AddDays(4)));
        Assert.Equal("A", RoutineResolver.For(d, Mon.AddDays(7))!.Name);
    }

    [Fact]
    public void Resolver_OverrideWins_OverSchedule()
    {
        var (d, _, b, c) = Setup();
        d.Schedules.Add(new RoutineSchedule { RoutineId = b.Id, WeekStart = Mon });
        d.WeekOverrides.Add(new WeekOverride { RoutineId = c.Id, WeekStart = Mon });
        Assert.Same(c, RoutineResolver.For(d, Mon.AddDays(6))); // Sunday belongs to the Monday week
    }

    [Fact]
    public void SetManual_InScheduledWeek_WritesOverride_AndReplaces()
    {
        var (d, _, b, c) = Setup();
        d.Schedules.Add(new RoutineSchedule { RoutineId = b.Id, WeekStart = Mon });
        RoutineResolver.SetManual(d, c.Id, Mon.AddDays(2));
        RoutineResolver.SetManual(d, d.Routines[0].Id, Mon.AddDays(3));
        Assert.Single(d.WeekOverrides);
        Assert.Equal(d.Routines[0].Id, d.ActiveRoutineId);
        Assert.Same(d.Routines[0], RoutineResolver.For(d, Mon));
    }

    [Fact]
    public void SetManual_NoSchedule_OnlySetsActive()
    {
        var (d, _, b, _) = Setup();
        RoutineResolver.SetManual(d, b.Id, Mon);
        Assert.Empty(d.WeekOverrides);
        Assert.Equal(b.Id, d.ActiveRoutineId);
    }
}

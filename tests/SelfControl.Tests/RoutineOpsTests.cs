using SelfControl.Core;

namespace SelfControl.Tests;

public class RoutineOpsTests
{
    static RoutineBlock Blk(int s, int e, DayOfWeek d = DayOfWeek.Monday) =>
        new() { Day = d, StartMin = s, EndMin = e };

    [Fact]
    public void Create_At1600_EndsAt1700()
    {
        var b = RoutineOps.Create(DayOfWeek.Monday, 960);
        Assert.Equal(960, b.StartMin);
        Assert.Equal(1020, b.EndMin);
    }

    [Fact]
    public void ResizeBottom_To1830_Gives150Min()
    {
        var b = Blk(960, 1020);
        RoutineOps.ResizeBottom(b, 1110);
        Assert.Equal(960, b.StartMin);
        Assert.Equal(1110, b.EndMin);
        Assert.Equal(150, b.EndMin - b.StartMin);
    }

    [Fact]
    public void ResizeBottom_Snaps_1837To1830()
    {
        var b = Blk(960, 1020);
        RoutineOps.ResizeBottom(b, 1117);
        Assert.Equal(1110, b.EndMin);
    }

    [Fact]
    public void ResizeTop_CannotPassEndMinus15()
    {
        var b = Blk(960, 1020);
        RoutineOps.ResizeTop(b, 1200);
        Assert.Equal(1005, b.StartMin);
        Assert.Equal(1020, b.EndMin);
    }

    [Fact]
    public void Move_KeepsLength_ClampsAtMidnight()
    {
        var b = Blk(600, 720);
        RoutineOps.Move(b, DayOfWeek.Tuesday, 1380);
        Assert.Equal(DayOfWeek.Tuesday, b.Day);
        Assert.Equal(1320, b.StartMin);
        Assert.Equal(1440, b.EndMin);
    }

    [Fact]
    public void OverlapColumns_TwoOverlapping_TwoLanes()
    {
        var lanes = RoutineOps.OverlapColumns(new[] { Blk(600, 720), Blk(660, 780) });
        Assert.Equal(2, lanes.Count);
    }

    [Fact]
    public void OverlapColumns_Sequential_OneLane()
    {
        var lanes = RoutineOps.OverlapColumns(new[] { Blk(600, 660), Blk(660, 720) });
        Assert.Single(lanes);
    }
}

public class DeleteRoutineTests
{
    [Fact]
    public void DeleteRoutine_RemovesSchedulesOverridesAndActive()
    {
        var a = new Routine { Name = "A" }; var b = new Routine { Name = "B" };
        var d = new AppData { ActiveRoutineId = a.Id };
        d.Routines.AddRange(new[] { a, b });
        var wk = new DateOnly(2026, 11, 2);
        d.Schedules.Add(new RoutineSchedule { RoutineId = a.Id, WeekStart = wk });
        d.Schedules.Add(new RoutineSchedule { RoutineId = b.Id, WeekStart = wk.AddDays(7) });
        d.WeekOverrides.Add(new WeekOverride { RoutineId = a.Id, WeekStart = wk });
        RoutineOps.DeleteRoutine(d, a.Id);
        Assert.Equal(new[] { b }, d.Routines);
        Assert.Null(d.ActiveRoutineId);
        Assert.Single(d.Schedules);
        Assert.Empty(d.WeekOverrides);
    }

    [Fact]
    public void DeleteRoutine_KeepsActiveWhenOther()
    {
        var a = new Routine(); var b = new Routine();
        var d = new AppData { ActiveRoutineId = b.Id };
        d.Routines.AddRange(new[] { a, b });
        RoutineOps.DeleteRoutine(d, a.Id);
        Assert.Equal(b.Id, d.ActiveRoutineId);
    }
}

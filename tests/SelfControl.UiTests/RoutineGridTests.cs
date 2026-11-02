using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class RoutineGridTests
{
    const double ColW = 100;
    static double X(DayOfWeek d) => RoutineGrid.LeftW + RoutineGrid.ColumnOf(d) * ColW + ColW / 2;
    static double Y(int min) => min * RoutineGrid.HourH / 60.0;

    static (Window w, RoutineGrid g, Routine r) Make(params RoutineBlock[] blocks)
    {
        var r = new Routine { Name = "R" };
        r.Blocks.AddRange(blocks);
        var g = new RoutineGrid { Width = RoutineGrid.LeftW + 7 * ColW, Height = 24 * RoutineGrid.HourH, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Routine = r };
        var w = new Window { Width = 800, Height = 1200, Content = g };
        w.Show();
        return (w, g, r);
    }

    [AvaloniaFact]
    public void Click_At1600_Creates1600To1700()
    {
        var (w, g, r) = Make();
        try
        {
            int changed = 0; g.Changed += () => changed++;
            var p = new Point(X(DayOfWeek.Wednesday), Y(960));
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            var b = Assert.Single(r.Blocks);
            Assert.Equal(DayOfWeek.Wednesday, b.Day);
            Assert.Equal((960, 1020), (b.StartMin, b.EndMin));
            Assert.Equal("Nova atividade", b.Title);
            Assert.True(changed >= 1);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void DragBottomEdge_To1830()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 960, EndMin = 1080 });
        try
        {
            var down = new Point(X(DayOfWeek.Monday), Y(1080) - 3);
            w.MouseDown(down, MouseButton.Left);
            w.MouseMove(new Point(down.X, Y(1110)), RawInputModifiers.LeftMouseButton);
            w.MouseUp(new Point(down.X, Y(1110)), MouseButton.Left);
            Assert.Equal((960, 1110), (r.Blocks[0].StartMin, r.Blocks[0].EndMin));
            Assert.Single(r.Blocks);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void DragTopEdge_Up_ExtendsStart()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 960, EndMin = 1080 });
        try
        {
            var down = new Point(X(DayOfWeek.Monday), Y(960) + 2);
            w.MouseDown(down, MouseButton.Left);
            w.MouseMove(new Point(down.X, Y(900)), RawInputModifiers.LeftMouseButton);
            w.MouseUp(new Point(down.X, Y(900)), MouseButton.Left);
            Assert.Equal((900, 1080), (r.Blocks[0].StartMin, r.Blocks[0].EndMin));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void DragBody_ToTuesday_MovesDay()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 960, EndMin = 1080 });
        try
        {
            var down = new Point(X(DayOfWeek.Monday), Y(1020));
            var up = new Point(X(DayOfWeek.Tuesday), Y(1020));
            w.MouseDown(down, MouseButton.Left);
            w.MouseMove(up, RawInputModifiers.LeftMouseButton);
            w.MouseUp(up, MouseButton.Left);
            Assert.Equal(DayOfWeek.Tuesday, r.Blocks[0].Day);
            Assert.Equal((960, 1080), (r.Blocks[0].StartMin, r.Blocks[0].EndMin));
        }
        finally { w.Close(); }
    }

    [Fact]
    public void Geometry_PureMethods()
    {
        Assert.Equal(960, RoutineGrid.MinuteAt(768));
        Assert.Equal(0, RoutineGrid.ColumnOf(DayOfWeek.Monday));
        Assert.Equal(6, RoutineGrid.ColumnOf(DayOfWeek.Sunday));
        Assert.Equal(DayOfWeek.Sunday, RoutineGrid.DayOfColumn(6));
        Assert.Equal(1, RoutineGrid.ColumnAt(RoutineGrid.LeftW + 150, RoutineGrid.LeftW + 700));
        Assert.Equal(6, RoutineGrid.ColumnAt(5000, RoutineGrid.LeftW + 700));
    }
}

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

    [AvaloniaFact]
    public void CaptureLost_MidDrag_EndsDragAndRaisesChangedOnce()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 960, EndMin = 1080 });
        try
        {
            int changed = 0; g.Changed += () => changed++;
            var down = new Point(X(DayOfWeek.Monday), Y(1080) - 3);
            w.MouseDown(down, MouseButton.Left);
            w.MouseMove(new Point(down.X, Y(1110)), RawInputModifiers.LeftMouseButton);
            var ptr = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            g.RaiseEvent(new PointerCaptureLostEventArgs(g, ptr));
            Assert.Equal(1, changed);
            w.MouseMove(new Point(down.X, Y(1200)), RawInputModifiers.LeftMouseButton);
            w.MouseUp(new Point(down.X, Y(1200)), MouseButton.Left);
            Assert.Equal(1110, r.Blocks[0].EndMin);
            Assert.Equal(1, changed);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void PlainClickOnBlock_DoesNotRaiseChanged()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 960, EndMin = 1080 });
        try
        {
            int changed = 0; g.Changed += () => changed++;
            var p = new Point(X(DayOfWeek.Monday), Y(1020));
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            Assert.Equal(0, changed);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void DoubleClickOnEmpty_CreatesExactlyOne()
    {
        var (w, g, r) = Make();
        try
        {
            var p = new Point(X(DayOfWeek.Friday), Y(600));
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            Assert.Single(r.Blocks);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void FifteenMinuteBlock_MovesByBody_AndResizesFromBottom()
    {
        var (w, g, r) = Make(new RoutineBlock { Day = DayOfWeek.Monday, StartMin = 600, EndMin = 615 });
        try
        {
            var b = r.Blocks[0];
            var down = new Point(X(DayOfWeek.Monday), Y(607));   // middle of a 12px block
            Assert.Equal(GridZone.Body, g.HitTest(down).zone);
            var to = new Point(X(DayOfWeek.Monday), Y(667));
            w.MouseDown(down, MouseButton.Left);
            w.MouseMove(to, RawInputModifiers.LeftMouseButton); w.MouseUp(to, MouseButton.Left);
            Assert.Equal(660, b.StartMin); Assert.Equal(675, b.EndMin);
            var bottom = new Point(X(DayOfWeek.Monday), Y(675) - 1);
            Assert.Equal(GridZone.Bottom, g.HitTest(bottom).zone);
            w.MouseDown(bottom, MouseButton.Left);
            var to2 = new Point(bottom.X, Y(720));
            w.MouseMove(to2, RawInputModifiers.LeftMouseButton); w.MouseUp(to2, MouseButton.Left);
            Assert.Equal((660, 720), (b.StartMin, b.EndMin));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void AtivaToggle_ReflectsRoutineInEffect_NotJustManualActive()
    {
        var a = new Routine { Name = "A" }; var b = new Routine { Name = "B" };
        var d = new AppData { ActiveRoutineId = a.Id };
        d.Routines.AddRange(new[] { a, b });
        var today = new DateOnly(2026, 11, 4);
        d.Schedules.Add(new RoutineSchedule { RoutineId = b.Id, WeekStart = RoutineResolver.WeekStart(today) });
        var v = new RoutineView(d, null) { TodayProvider = () => today };
        var picker = (ComboBox)((DockPanel)v.Content!).Children.OfType<StackPanel>().First().Children[0];
        picker.SelectedItem = a;
        Assert.False(v.ActiveToggle.IsChecked);
        picker.SelectedItem = b;
        Assert.True(v.ActiveToggle.IsChecked);
        picker.SelectedItem = a;
        v.ActiveToggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(v.ActiveToggle.IsChecked);
        Assert.Equal(a.Id, RoutineResolver.For(d, today)!.Id);
    }
}

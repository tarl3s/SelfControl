using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

public enum GridZone { None, Top, Body, Bottom }

/// <summary>Weekly grid: 7 day columns (Mon first), 00–24 h. Click empty to create, drag edges to resize, drag body to move.</summary>
public class RoutineGrid : Control
{
    public const double HourH = 48, LeftW = 48, EdgePx = 6;

    static readonly IBrush Line = new SolidColorBrush(Color.Parse("#262626"));
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    static readonly IBrush NoteBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
    static readonly IBrush DefaultFill = new SolidColorBrush(Color.Parse("#1A1A1A"));
    static readonly Pen LinePen = new(Line, 1);
    // Marks the current time on today's column; kept faint so it doesn't pull the eye.
    static readonly Pen NowPen = new(new SolidColorBrush(Color.Parse("#4D4D4D")), 1);
    static readonly Pen SelPen = new(Brushes.White, 2);
    static readonly Pen BlockPen = new(new SolidColorBrush(Color.Parse("#3A3A3A")), 1);
    static readonly FontFamily Font = new("Segoe UI, Inter, sans-serif");

    public Routine? Routine { get => _routine; set { _routine = value; Selected = null; _drag = null; InvalidateVisual(); } }
    Routine? _routine;
    public RoutineBlock? Selected { get; private set; }
    public void Select(RoutineBlock? b) { Selected = b; InvalidateVisual(); }
    public Func<DateTime> Now { get; set; } = () => DateTime.Now;
    /// <summary>Raised after a change is committed (create, end of drag, edit) so the owner saves.</summary>
    public event Action? Changed;
    public event Action<RoutineBlock>? EditRequested;

    (RoutineBlock b, GridZone zone, int grab, DayOfWeek day, int s, int e)? _drag;

    Point? _hover;

    public RoutineGrid() { ClipToBounds = true; }

    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); _hover = null; }

    /// <summary>Ctrl+V: adds a copy of <paramref name="src"/> under the mouse, or right after the selected block (else the original) on the same day.</summary>
    public RoutineBlock? Paste(RoutineBlock src)
    {
        if (_routine == null) return null;
        var nb = new RoutineBlock { Title = src.Title, Color = src.Color, Note = src.Note, Day = src.Day, StartMin = src.StartMin, EndMin = src.EndMin };
        var after = Selected ?? src;
        if (_hover is { } p) RoutineOps.Move(nb, DayOfColumn(ColumnAt(p.X, Bounds.Width)), RoutineOps.SnapMin(MinuteAt(p.Y)));
        else RoutineOps.Move(nb, after.Day, after.EndMin);
        _routine.Blocks.Add(nb);
        Selected = nb;
        InvalidateVisual();
        Changed?.Invoke();
        return nb;
    }

    // ---- pure geometry ----
    public static int MinuteAt(double y) => (int)Math.Round(y * 60.0 / HourH);
    public static double YAt(int min) => min * HourH / 60.0;
    public static double ColWidth(double width) => Math.Max(1, (width - LeftW) / 7);
    public static int ColumnAt(double x, double width) => Math.Clamp((int)Math.Floor((x - LeftW) / ColWidth(width)), 0, 6);
    public static int ColumnOf(DayOfWeek d) => ((int)d + 6) % 7;
    public static DayOfWeek DayOfColumn(int c) => (DayOfWeek)((c + 1) % 7);
    public static string Hm(int min) => $"{min / 60:00}:{min % 60:00}";

    public Rect BlockRect(RoutineBlock b)
    {
        double cw = ColWidth(Bounds.Width);
        var lanes = RoutineOps.OverlapColumns(_routine!.Blocks.Where(x => x.Day == b.Day));
        int n = Math.Max(1, lanes.Count), i = Math.Max(0, lanes.FindIndex(l => l.Contains(b)));
        double lw = cw / n;
        double x = LeftW + ColumnOf(b.Day) * cw + i * lw;
        return new Rect(x + 1, YAt(b.StartMin) + 1, lw - 2, Math.Max(2, YAt(b.EndMin - b.StartMin) - 2));
    }

    public (RoutineBlock? block, GridZone zone) HitTest(Point p)
    {
        if (_routine == null) return (null, GridZone.None);
        for (int i = _routine.Blocks.Count - 1; i >= 0; i--)
        {
            var b = _routine.Blocks[i];
            var r = BlockRect(b);
            if (!r.Contains(p)) continue;
            double edge = Math.Min(EdgePx, r.Height / 3);
            var zone = p.Y - r.Top < edge ? GridZone.Top : r.Bottom - p.Y < edge ? GridZone.Bottom : GridZone.Body;
            return (b, zone);
        }
        return (null, GridZone.None);
    }

    // ---- pointer ----
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_routine == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        if (p.X < LeftW) return;
        var (b, zone) = HitTest(p);
        if (b == null)
        {
            if (e.ClickCount > 1) return;
            var nb = RoutineOps.Create(DayOfColumn(ColumnAt(p.X, Bounds.Width)), MinuteAt(p.Y));
            nb.Title = L.T("New activity");
            _routine.Blocks.Add(nb);
            Selected = nb;
            InvalidateVisual();
            Changed?.Invoke();
            return;
        }
        Selected = b;
        if (e.ClickCount == 2) { InvalidateVisual(); EditRequested?.Invoke(b); return; }
        _drag = (b, zone, MinuteAt(p.Y) - b.StartMin, b.Day, b.StartMin, b.EndMin);
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _hover = p.X >= LeftW ? p : null;
        if (_drag is not { } d)
        {
            var z = HitTest(p).zone;
            Cursor = z is GridZone.Top or GridZone.Bottom ? new Cursor(StandardCursorType.SizeNorthSouth)
                   : z == GridZone.Body ? new Cursor(StandardCursorType.SizeAll) : null;
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { EndDrag(); return; }
        switch (d.zone)
        {
            case GridZone.Top: RoutineOps.ResizeTop(d.b, MinuteAt(p.Y)); break;
            case GridZone.Bottom: RoutineOps.ResizeBottom(d.b, MinuteAt(p.Y)); break;
            default: RoutineOps.Move(d.b, DayOfColumn(ColumnAt(p.X, Bounds.Width)), MinuteAt(p.Y) - d.grab); break;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag();
    }

    void EndDrag()
    {
        if (_drag is not { } d) return;
        _drag = null;
        if (d.b.Day != d.day || d.b.StartMin != d.s || d.b.EndMin != d.e) Changed?.Invoke();
    }

    /// <summary>Called by the owner after editing/deleting a block outside the grid.</summary>
    public void Refresh(bool deselect = false) { if (deselect) Selected = null; InvalidateVisual(); }

    // ---- drawing ----
    static FormattedText Text(string s, double size, IBrush brush, double maxW = 10000, FontWeight w = FontWeight.Normal) =>
        new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(Font, FontStyle.Normal, w), size, brush)
        { MaxTextWidth = Math.Max(1, maxW), MaxTextHeight = size * 1.5, Trimming = TextTrimming.CharacterEllipsis };

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, cw = ColWidth(w);
        ctx.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        for (int h = 0; h <= 24; h++)
        {
            double y = h * HourH;
            ctx.DrawLine(LinePen, new Point(LeftW, y), new Point(w, y));
            if (h < 24) ctx.DrawText(Text($"{h:00}:00", 11, Dim), new Point(4, y + 2));
        }
        for (int c = 0; c <= 7; c++) ctx.DrawLine(LinePen, new Point(LeftW + c * cw, 0), new Point(LeftW + c * cw, 24 * HourH));
        if (_routine != null)
            foreach (var b in _routine.Blocks)
            {
                var r = BlockRect(b);
                IBrush fill = DefaultFill;
                if (b.Color is { } cs && Color.TryParse(cs, out var col)) fill = new SolidColorBrush(col);
                ctx.DrawRectangle(fill, b == Selected ? SelPen : BlockPen, r, 4, 4);
                var inner = r.Deflate(new Thickness(5, 2));
                if (inner.Width > 8 && inner.Height > 8)
                {
                    ctx.DrawText(Text(b.Title, 12, Brushes.White, inner.Width, FontWeight.SemiBold), inner.TopLeft);
                    double y = inner.Y + 16, room = inner.Bottom - y;
                    if (!string.IsNullOrWhiteSpace(b.Note) && room > 12)
                    {
                        var nt = Text(b.Note, 11, NoteBrush, inner.Width);
                        nt.MaxTextHeight = Math.Max(13, room - (room > 44 ? 15 : 0));
                        ctx.DrawText(nt, new Point(inner.X, y));
                        y += nt.Height + 1;
                    }
                    if (inner.Bottom - y > 13)
                        ctx.DrawText(Text($"{Hm(b.StartMin)}–{Hm(b.EndMin)}", 11, Dim, inner.Width), new Point(inner.X, y));
                }
            }
        var now = Now();
        double ny = YAt(now.Hour * 60 + now.Minute);
        int c0 = ColumnOf(now.DayOfWeek);
        ctx.DrawLine(NowPen, new Point(LeftW + c0 * cw, ny), new Point(LeftW + (c0 + 1) * cw, ny));
    }
}

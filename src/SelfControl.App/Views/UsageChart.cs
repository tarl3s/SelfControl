using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>Vertical bars (one per day) with the time on top and the label below; today's bar is white.</summary>
public class UsageChart : Control
{
    static readonly IBrush Bar = new SolidColorBrush(Color.Parse("#5A5A5A"));
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#262626")), 1);
    IReadOnlyList<(string Label, TimeSpan Time, bool Today)> _bars = Array.Empty<(string, TimeSpan, bool)>();

    public IReadOnlyList<(string Label, TimeSpan Time, bool Today)> Bars
    {
        get => _bars;
        set { _bars = value; InvalidateVisual(); }
    }

    static FormattedText Text(string s, double size, IBrush b) =>
        new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b);

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_bars.Count == 0 || w < 10 || h < 60) return;
        const double top = 20, bottom = 22;
        double plot = h - top - bottom;
        double maxH = Math.Max(1, Math.Ceiling(_bars.Max(b => b.Time.TotalHours)));
        for (int i = 0; i <= (int)maxH; i++)
        {
            double y = top + plot - plot * i / maxH;
            ctx.DrawLine(GridPen, new Point(0, y), new Point(w, y));
        }
        double slot = w / _bars.Count, bw = Math.Max(2, Math.Min(48, slot * 0.6));
        bool small = slot < 40;
        for (int i = 0; i < _bars.Count; i++)
        {
            var (label, t, today) = _bars[i];
            double cx = slot * i + slot / 2, bh = plot * t.TotalHours / maxH;
            if (bh > 0) ctx.DrawRectangle(today ? Brushes.White : Bar, null, new Rect(cx - bw / 2, top + plot - bh, bw, bh), 3, 3);
            if (!small && t > TimeSpan.Zero)
            {
                var v = Text(Usage.Format(t), 11, Dim);
                ctx.DrawText(v, new Point(cx - v.Width / 2, top + plot - bh - 16));
            }
            if (!small || i % 7 == 0 || today)
            {
                var l = Text(label, 11, today ? Brushes.White : Dim);
                ctx.DrawText(l, new Point(cx - l.Width / 2, h - bottom + 4));
            }
        }
    }
}

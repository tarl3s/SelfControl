namespace SelfControl.Core;

public static class RoutineOps
{
    public const int Snap = 15, DefaultLen = 60, DayLen = 1440;

    public static int SnapMin(int m) => (int)Math.Round(m / (double)Snap, MidpointRounding.AwayFromZero) * Snap;

    public static RoutineBlock Create(DayOfWeek d, int clickMin)
    {
        int start = Math.Clamp(SnapMin(clickMin), 0, DayLen - Snap);
        return new RoutineBlock { Day = d, StartMin = start, EndMin = Math.Min(start + DefaultLen, DayLen) };
    }

    public static void ResizeTop(RoutineBlock b, int newStart) =>
        b.StartMin = Math.Clamp(SnapMin(newStart), 0, b.EndMin - Snap);

    public static void ResizeBottom(RoutineBlock b, int newEnd) =>
        b.EndMin = Math.Clamp(SnapMin(newEnd), b.StartMin + Snap, DayLen);

    public static void Move(RoutineBlock b, DayOfWeek day, int newStart)
    {
        int len = b.EndMin - b.StartMin;
        b.Day = day;
        b.StartMin = Math.Clamp(SnapMin(newStart), 0, DayLen - len);
        b.EndMin = b.StartMin + len;
    }

    /// <summary>Greedy lanes: blocks in the same lane never overlap; overlapping ones go side by side.</summary>
    public static List<List<RoutineBlock>> OverlapColumns(IEnumerable<RoutineBlock> dayBlocks)
    {
        var lanes = new List<List<RoutineBlock>>();
        foreach (var b in dayBlocks.OrderBy(x => x.StartMin).ThenBy(x => x.EndMin))
        {
            var lane = lanes.FirstOrDefault(l => l[^1].EndMin <= b.StartMin);
            if (lane == null) lanes.Add(lane = new List<RoutineBlock>());
            lane.Add(b);
        }
        return lanes;
    }
}

namespace SelfControl.Core;

/// <summary>
/// Counts time the computer is in use and says when an hour has passed. A gap longer than 5 minutes between ticks
/// (sleep, PC off) starts the hour over, so waking the PC doesn't pop a suggestion straight away.
/// </summary>
public sealed class BreakClock
{
    public static readonly TimeSpan Every = TimeSpan.FromHours(1);
    static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(5);
    DateTime _last;
    TimeSpan _used;

    public BreakClock(DateTime now) => _last = now;

    /// <summary>Call regularly (e.g. every 30 s). True once per hour of use.</summary>
    public bool Tick(DateTime now)
    {
        var gap = now - _last;
        _last = now;
        if (gap < TimeSpan.Zero || gap > MaxGap) { _used = TimeSpan.Zero; return false; }
        _used += gap;
        if (_used < Every) return false;
        _used = TimeSpan.Zero;
        return true;
    }
}

public record ExerciseStep(string Title, string Text, int Seconds);

/// <summary>The stretches shown after "Stretch" (English text = key; the app translates it) (one step at a time, with a countdown).</summary>
public static class Exercises
{
    public static readonly IReadOnlyList<ExerciseStep> Stretches = new ExerciseStep[]
    {
        new("Neck", "Tilt your head to the right, bringing your ear toward your shoulder. Breathe slowly.", 15),
        new("Neck", "Now to the left.", 15),
        new("Shoulders", "Roll your shoulders backward in big circles.", 15),
        new("Arms", "Interlace your fingers and stretch your arms above your head, palms up.", 15),
        new("Chest", "Clasp your hands behind your back and open your chest, pulling your shoulders back.", 15),
        new("Back", "Sitting, twist your torso to the right, holding the back of the chair.", 15),
        new("Back", "Now twist to the left.", 15),
        new("Wrists", "Extend your right arm and pull your fingers back with your other hand.", 15),
        new("Wrists", "Now the left arm.", 15),
        new("Legs", "Stand up and walk around a little, or rise onto your toes a few times.", 30),
    };
}

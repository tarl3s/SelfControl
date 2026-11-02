namespace SelfControl.Core;

public enum BlockPhase { Free, Warning, Break, Expired }

/// <summary>Per app/site breaktime state. Closing the blocked thing forgets it, so reopening warns again.</summary>
public class BreaktimeTracker
{
    readonly Func<DateTime> _now;
    // Value: break end time, or null while only warned. Expired is kept as an end in the past.
    readonly Dictionary<string, DateTime?> _state = new(StringComparer.OrdinalIgnoreCase);

    public BreaktimeTracker(Func<DateTime> now) => _now = now;

    public BlockPhase OnSeen(string key, bool locked)
    {
        if (!locked) return BlockPhase.Free;
        if (!_state.TryGetValue(key, out var end)) { _state[key] = null; return BlockPhase.Warning; }
        if (end is null) return BlockPhase.Warning;
        return _now() < end.Value ? BlockPhase.Break : BlockPhase.Expired;
    }

    public void StartBreak(string key, int minutes)
    {
        if (minutes < 1 || minutes > 120) return;
        if (_state.TryGetValue(key, out var end) && end is null)
            _state[key] = _now().AddMinutes(minutes);
    }

    public void OnClosed(string key) => _state.Remove(key);

    public TimeSpan? Remaining(string key)
    {
        if (_state.TryGetValue(key, out var end) && end is { } e && _now() < e) return e - _now();
        return null;
    }
}

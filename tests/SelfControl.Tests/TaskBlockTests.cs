using SelfControl.Core;

namespace SelfControl.Tests;

public class TaskBlockTests
{
    static readonly DateOnly Tue = new(2026, 11, 3);
    static readonly DateOnly NextTue = new(2026, 11, 10);

    [Fact]
    public void Temp_TaskOnlyToday()
    {
        var data = new AppData();
        data.Tasks.Add(new TaskItem { Title = "t", TempDate = Tue });
        Assert.Single(TaskRules.Today(data, Tue));
        Assert.Empty(TaskRules.Today(data, Tue.AddDays(1)));
    }

    [Fact]
    public void Rule_Tuesday_AppearsUncheckedEachTuesday()
    {
        var data = new AppData();
        var t = new TaskItem { Title = "r", Rule = DateRule.Every(DayOfWeek.Tuesday) };
        data.Tasks.Add(t);
        TaskRules.SetDone(t, Tue, true);
        Assert.True(TaskRules.IsDone(t, Tue));
        Assert.Single(TaskRules.Today(data, NextTue));
        Assert.False(TaskRules.IsDone(t, NextTue));
        TaskRules.SetDone(t, Tue, false);
        Assert.False(TaskRules.IsDone(t, Tue));
    }

    [Fact]
    public void Locked_UntilAllDone()
    {
        var data = new AppData();
        var a = new TaskItem { TempDate = Tue };
        var b = new TaskItem { TempDate = Tue };
        data.Tasks.AddRange(new[] { a, b });
        Assert.True(TaskRules.IsLocked(data, Tue));
        TaskRules.SetDone(a, Tue, true);
        Assert.True(TaskRules.IsLocked(data, Tue));
        TaskRules.SetDone(b, Tue, true);
        Assert.False(TaskRules.IsLocked(data, Tue));
    }

    [Fact]
    public void NoTasks_NotLocked() => Assert.False(TaskRules.IsLocked(new AppData(), Tue));

    [Fact]
    public void Site_Subdomain_Matches_Lookalike_DoesNot()
    {
        Assert.True(BlockRules.MatchesSite("youtube.com", "youtube.com"));
        Assert.True(BlockRules.MatchesSite("m.youtube.com", "youtube.com"));
        Assert.True(BlockRules.MatchesSite("WWW.YouTube.com", "youtube.com"));
        Assert.False(BlockRules.MatchesSite("notyoutube.com", "youtube.com"));
        Assert.False(BlockRules.MatchesSite("youtube.com.evil.io", "youtube.com"));
    }

    [Fact]
    public void NormalizeApp_TakesFileNameLowercase()
    {
        Assert.Equal("steam.exe", BlockRules.NormalizeApp(@"C:\Program Files\Steam\Steam.EXE"));
        Assert.Equal("steam.exe", BlockRules.NormalizeApp("Steam"));
    }

    [Fact]
    public void CannotBlock_ProtectedExe()
    {
        Assert.False(BlockRules.CanBlockApp("selfcontrol.exe"));
        Assert.False(BlockRules.CanBlockApp(@"C:\Windows\Explorer.EXE"));
        Assert.False(BlockRules.CanBlockApp("brave.exe"));
        Assert.True(BlockRules.CanBlockApp("steam.exe"));
    }

    static (BreaktimeTracker t, Func<DateTime> advance) Make(out DateTime[] clock)
    {
        var c = new[] { new DateTime(2026, 11, 3, 10, 0, 0) };
        clock = c;
        return (new BreaktimeTracker(() => c[0]), () => c[0]);
    }

    [Fact]
    public void Break_Expires_ToExpired()
    {
        var (t, _) = Make(out var clk);
        Assert.Equal(BlockPhase.Warning, t.OnSeen("a.exe", true));
        t.StartBreak("a.exe", 5);
        Assert.Equal(BlockPhase.Break, t.OnSeen("a.exe", true));
        Assert.Equal(TimeSpan.FromMinutes(5), t.Remaining("a.exe"));
        clk[0] = clk[0].AddMinutes(5);
        Assert.Equal(BlockPhase.Expired, t.OnSeen("a.exe", true));
        Assert.Null(t.Remaining("a.exe"));
    }

    [Fact]
    public void Expired_CannotStartBreak()
    {
        var (t, _) = Make(out var clk);
        t.OnSeen("a.exe", true);
        t.StartBreak("a.exe", 1);
        clk[0] = clk[0].AddMinutes(2);
        t.OnSeen("a.exe", true);
        t.StartBreak("a.exe", 5);
        Assert.Equal(BlockPhase.Expired, t.OnSeen("a.exe", true));
    }

    [Fact]
    public void StartBreak_RejectsOutOfRangeMinutes()
    {
        var (t, _) = Make(out _);
        t.OnSeen("a.exe", true);
        t.StartBreak("a.exe", 0);
        t.StartBreak("a.exe", 121);
        Assert.Equal(BlockPhase.Warning, t.OnSeen("a.exe", true));
    }

    [Fact]
    public void Closed_ThenReopen_IsWarningAgain()
    {
        var (t, _) = Make(out var clk);
        t.OnSeen("a.exe", true);
        t.StartBreak("a.exe", 1);
        clk[0] = clk[0].AddMinutes(2);
        Assert.Equal(BlockPhase.Expired, t.OnSeen("a.exe", true));
        t.OnClosed("a.exe");
        Assert.Equal(BlockPhase.Warning, t.OnSeen("a.exe", true));
    }

    [Fact]
    public void Break_PerKey_OtherStillWarning()
    {
        var (t, _) = Make(out _);
        t.OnSeen("a.exe", true);
        t.OnSeen("b.com", true);
        t.StartBreak("a.exe", 5);
        Assert.Equal(BlockPhase.Break, t.OnSeen("a.exe", true));
        Assert.Equal(BlockPhase.Warning, t.OnSeen("b.com", true));
    }

    [Fact]
    public void Unlocked_IsFreeEvenDuringWarning()
    {
        var (t, _) = Make(out _);
        Assert.Equal(BlockPhase.Warning, t.OnSeen("a.exe", true));
        Assert.Equal(BlockPhase.Free, t.OnSeen("a.exe", false));
    }
}

public class BreaktimeClearTests
{
    [Fact]
    public void Clear_ForgetsWarningsAndBreaks()
    {
        var now = new DateTime(2026, 1, 1, 10, 0, 0);
        var t = new SelfControl.Core.BreaktimeTracker(() => now);
        t.OnSeen("a", true); t.StartBreak("a", 5);
        Assert.NotNull(t.Remaining("a"));
        t.Clear();
        Assert.Null(t.Remaining("a"));
        Assert.Equal(SelfControl.Core.BlockPhase.Warning, t.OnSeen("a", true));
    }
}

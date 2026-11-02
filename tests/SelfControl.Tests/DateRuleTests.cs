using SelfControl.Core;

namespace SelfControl.Tests;

public class DateRuleTests
{
    [Fact]
    public void Weekdays_MatchesMonToFri_NotSat()
    {
        var rule = DateRule.Weekdays();
        // 2026-11-02 is a Monday.
        for (int i = 0; i < 5; i++) Assert.True(rule.Matches(new DateOnly(2026, 11, 2).AddDays(i)));
        Assert.False(rule.Matches(new DateOnly(2026, 11, 7)));
        Assert.False(rule.Matches(new DateOnly(2026, 11, 8)));
    }

    [Fact]
    public void On_MatchesOnlyThatDate()
    {
        var rule = DateRule.On(new DateOnly(2026, 11, 2));
        Assert.True(rule.Matches(new DateOnly(2026, 11, 2)));
        Assert.False(rule.Matches(new DateOnly(2026, 11, 9)));
        Assert.False(rule.Matches(new DateOnly(2026, 11, 3)));
    }

    [Fact]
    public void Weekend_MatchesSatSun()
    {
        var rule = DateRule.Weekend();
        Assert.True(rule.Matches(new DateOnly(2026, 11, 7)));
        Assert.True(rule.Matches(new DateOnly(2026, 11, 8)));
        Assert.False(rule.Matches(new DateOnly(2026, 11, 9)));
    }
}

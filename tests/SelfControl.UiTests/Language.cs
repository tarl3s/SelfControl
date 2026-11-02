using System.Runtime.CompilerServices;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;
using Xunit;

// The language is process-wide state; tests that switch it must not overlap with the others.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SelfControl.UiTests;

static class TestLanguage
{
    /// <summary>The suites assert Portuguese texts, so they run in pt-BR regardless of the machine's language.</summary>
    [ModuleInitializer]
    internal static void Init() => L.Set(L.Portuguese);
}

public class LocTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelfControl.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("SelfControl.sln not found");
    }

    [Fact]
    public void EveryKeyUsedInTheSources_HasAPortugueseEntry()
    {
        var rx = new System.Text.RegularExpressions.Regex(@"\bL\.[TF]\(""((?:[^""\x5C]|\x5C.)*)""");
        var missing = new List<string>();
        var seen = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.EndsWith("Loc.cs")) continue;
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(File.ReadAllText(file)))
            {
                seen++;
                var key = m.Groups[1].Value.Replace(@"\""", "\"");
                if (!L.HasPortuguese(key) && key != "Breaktime" && key.Length > 0 && key.Any(char.IsLetter) && !IsSameInBoth(key)) missing.Add(key);
            }
        }
        Assert.True(seen > 80, "scanner found too few keys: " + seen);
        Assert.True(missing.Count == 0, "No pt-BR entry for: " + string.Join(" | ", missing.Distinct()));
    }

    // Words that are the same in English and Portuguese need no table entry.
    static bool IsSameInBoth(string key) => key is "Sites" or "OK" or "Breaktime";

    [Fact]
    public void ExerciseTexts_AllHavePortuguese()
    {
        foreach (var s in Exercises.Stretches)
        {
            Assert.True(L.HasPortuguese(s.Title), s.Title);
            Assert.True(L.HasPortuguese(s.Text), s.Text);
        }
        foreach (var p in BreakWindows.EyePhases) Assert.True(L.HasPortuguese(p.Text), p.Text);
    }

    [Fact]
    public void SwitchingToEnglish_GivesEnglishWarningAndSummaries()
    {
        try
        {
            L.Set(L.English);
            Assert.Equal("Finish your tasks before opening", BlockWarningWindow.FirstText);
            Assert.Equal("Breaktime is over, back to your tasks", BlockWarningWindow.ExpiredText);
            Assert.Equal("Mon–Fri 21:30", RuleText.Summary(DateRule.Weekdays(), new TimeOnly(21, 30)));
            Assert.Equal("Every day", RuleText.Days(DateRule.Every(Enum.GetValues<DayOfWeek>())));
            L.Set(L.Portuguese);
            Assert.Equal("Termine suas tarefas antes de abrir", BlockWarningWindow.FirstText);
            Assert.Equal("Seg–Sex 21:30", RuleText.Summary(DateRule.Weekdays(), new TimeOnly(21, 30)));
        }
        finally { L.Set(L.Portuguese); }
    }

    [Fact]
    public void Setting_NullFollowsWindows_AndPtPrefixIsPortuguese()
    {
        try
        {
            L.Set("pt-PT"); Assert.Equal(L.Portuguese, L.Current);
            L.Set("en"); Assert.Equal(L.English, L.Current);
            L.Set(null); Assert.Equal(L.FromCulture(), L.Current); Assert.Null(L.Setting);
        }
        finally { L.Set(L.Portuguese); }
    }
}

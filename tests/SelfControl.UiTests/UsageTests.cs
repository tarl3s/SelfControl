using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SelfControl.App;
using SelfControl.App.Views;
using SelfControl.Core;

namespace SelfControl.UiTests;

public class UsageTests
{
    sealed class NoBlocker : IBlocker
    {
        public event Action<string>? AppSeen { add { } remove { } }
        public event Action<string>? AppClosed { add { } remove { } }
        public void Kill(string exe) { }
        public void SetWatched(IEnumerable<string> exes) { }
    }
    sealed class NoNotifier : INotifier { public void Show(string t, string m) { } }
    sealed class NoAutostart : IAutostart { public bool Enabled { get; set; } }

    [AvaloniaFact]
    public void UsageView_ShowsChosenWeekOrFourWeeks_AndTopLists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-ui-" + Guid.NewGuid().ToString("N")[..8]);
        var today = new DateOnly(2026, 11, 5);
        var s = new AppState(new DataStore(dir), new NoBlocker(), new NoNotifier(), new NoAutostart(), now: () => today.ToDateTime(new TimeOnly(12, 0)));
        try
        {
            Usage.Add(s.Data, today, TimeSpan.FromHours(2), "Steam");
            Usage.Add(s.Data, today.AddDays(-7), TimeSpan.FromHours(1), "Brave", "youtube.com");
            var v = new UsageView(s, () => today);
            Assert.Equal(7, v.Chart.Bars.Count);
            Assert.Equal(TimeSpan.FromHours(2), v.Chart.Bars.Single(b => b.Today).Time);
            Assert.Equal("Steam", ((TextBlock)((Grid)v.Apps.Children[0]).Children[0]).Text);
            Assert.IsType<TextBlock>(v.Sites.Children[0]);                         // no sites this week
            Usage.Add(s.Data, today.AddDays(-1), TimeSpan.FromHours(3), "Yesterday");
            s.DataChanged();
            Assert.Single(v.Apps.Children);                                         // only today, not the week
            v.ClickBar(2);                                                          // Wednesday
            Assert.Equal("Yesterday", ((TextBlock)((Grid)v.Apps.Children[0]).Children[0]).Text);
            Assert.Single(v.Apps.Children);
            v.ClickBar(5);                                                          // a day to come: ignored
            Assert.Equal(2, v.Chart.Selected);
            v.ClickBar(2);                                                          // again: the whole week
            Assert.Equal(2, v.Apps.Children.Count);
            Assert.Null(v.Chart.Selected);
            v.Period.SelectedIndex = 1;                                             // last week
            Assert.Equal(TimeSpan.FromHours(1), v.Chart.Bars[3].Time);              // its Thursday
            Assert.Equal("youtube.com", ((TextBlock)((Grid)v.Sites.Children[0]).Children[0]).Text);
            v.Period.SelectedIndex = 4;                                             // last 4 weeks
            Assert.Equal(28, v.Chart.Bars.Count);
            Assert.Equal(3, v.Apps.Children.Count);
        }
        finally { s.Dispose(); try { Directory.Delete(dir, true); } catch { } }
    }
}

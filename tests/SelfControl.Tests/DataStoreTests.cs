using SelfControl.Core;

namespace SelfControl.Tests;

public class DataStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sc_test_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        var store = new DataStore(_dir);
        var data = new AppData();
        data.Events.Add(new ReminderEvent { Message = "m", Time = new TimeOnly(21, 30), Rule = DateRule.Every(DayOfWeek.Monday, DayOfWeek.Friday) });
        data.Tasks.Add(new TaskItem { Title = "t", Rule = DateRule.On(new DateOnly(2026, 11, 2)), DoneOn = { new DateOnly(2026, 11, 2) } });
        store.Save(data);
        var back = store.Load();
        Assert.Equal(new TimeOnly(21, 30), back.Events[0].Time);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, back.Events[0].Rule.Days);
        Assert.Equal(new DateOnly(2026, 11, 2), back.Tasks[0].Rule!.Date);
        Assert.Contains(new DateOnly(2026, 11, 2), back.Tasks[0].DoneOn);
    }

    [Fact]
    public void Load_Missing_ReturnsNew()
    {
        Assert.Empty(new DataStore(_dir).Load().Events);
    }

    [Fact]
    public void Corrupt_File_Renamed_StartsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "data.json"), "{ not json");
        var data = new DataStore(_dir).Load();
        Assert.Empty(data.Events);
        Assert.False(File.Exists(Path.Combine(_dir, "data.json")));
        Assert.Single(Directory.GetFiles(_dir, "data.corrupt-*.json"));
    }

    [Fact]
    public void Import_FutureVersion_Throws()
    {
        Directory.CreateDirectory(_dir);
        var p = Path.Combine(_dir, "in.json");
        File.WriteAllText(p, "{\"Version\":2}");
        Assert.Throws<InvalidDataException>(() => new DataStore(_dir).Import(p));
        File.WriteAllText(p, "garbage");
        Assert.Throws<InvalidDataException>(() => new DataStore(_dir).Import(p));
    }

    [Fact]
    public void Export_Import_RoundTrip()
    {
        var store = new DataStore(_dir);
        var data = new AppData();
        data.Events.Add(new ReminderEvent { Message = "e" });
        var p = Path.Combine(_dir, "sub", "out.json");
        store.Export(data, p);
        Assert.Equal("e", store.Import(p).Events[0].Message);
    }

    [Fact]
    public void Load_PartialJson_FillsNulls()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "data.json"),
            "{\"Version\":1,\"Events\":[{\"Message\":\"x\",\"Time\":\"21:30:00\",\"Rule\":{\"Date\":null}}],\"Routines\":null,\"BlockList\":null,\"Tasks\":[{\"Title\":\"t\",\"Rule\":{\"Date\":null,\"Days\":null},\"DoneOn\":null}]}");
        var d = new DataStore(_dir).Load();
        Assert.NotNull(d.Events[0].Rule.Days);
        Assert.NotNull(d.Tasks[0].Rule!.Days);
        Assert.NotNull(d.Tasks[0].DoneOn);
        Assert.NotNull(d.Routines);
        Assert.NotNull(d.Schedules);
        Assert.NotNull(d.WeekOverrides);
        Assert.NotNull(d.Tasks);
        Assert.NotNull(d.Home);
        Assert.NotNull(d.BlockList);
        Assert.NotNull(d.BlockList.Apps);
        Assert.NotNull(d.BlockList.Sites);
    }
}

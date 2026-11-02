using System.Text.Json;

namespace SelfControl.Core;

public class DataStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _folder;
    private string FilePath => Path.Combine(_folder, "data.json");

    public DataStore(string folder) => _folder = folder;

    /// <summary>The folder holding data.json; other per-user files (home images) live beside it.</summary>
    public string Folder => _folder;

    /// <summary>True when the last Load could not read an existing file; Save then does nothing so it is never overwritten.</summary>
    public bool LoadFailed { get; private set; }

    public AppData Load()
    {
        LoadFailed = false;
        if (!File.Exists(FilePath)) return new AppData();
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return Parse(File.ReadAllText(FilePath));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 2) { LoadFailed = true; return new AppData(); }
                Thread.Sleep(200);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                try
                {
                    File.Move(FilePath, Path.Combine(_folder, $"data.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json"), true);
                }
                catch (Exception m) when (m is IOException or UnauthorizedAccessException)
                {
                    LoadFailed = true;
                }
                return new AppData();
            }
        }
    }

    public void Save(AppData data)
    {
        if (LoadFailed) return;
        Directory.CreateDirectory(_folder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
        if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
        else File.Move(tmp, FilePath);
    }

    public void Export(AppData data, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, Options));
    }

    public AppData Import(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Not a valid SelfControl data file.", e);
        }
    }

    private static AppData Parse(string json)
    {
        var data = JsonSerializer.Deserialize<AppData>(json) ?? throw new InvalidDataException("Empty data file.");
        if (data.Version > 1) throw new InvalidDataException($"Unsupported data version {data.Version}.");
        Normalize(data);
        return data;
    }

    private static DateRule Fix(DateRule? r) => r == null ? new DateRule(null, Array.Empty<DayOfWeek>()) : r.Days == null ? r with { Days = Array.Empty<DayOfWeek>() } : r;

    private static List<T> Clean<T>(List<T>? list) where T : class => list == null ? new() : list.Where(x => x != null).ToList();

    private static void Normalize(AppData d)
    {
        d.Routines = Clean(d.Routines);
        d.Schedules = Clean(d.Schedules);
        d.WeekOverrides = Clean(d.WeekOverrides);
        d.Events = Clean(d.Events);
        d.Tasks = Clean(d.Tasks);
        d.Home = Clean(d.Home);
        d.BlockList ??= new BlockList();
        d.BlockList.Apps ??= new();
        d.BlockList.Sites ??= new();
        foreach (var r in d.Routines) r.Blocks = Clean(r.Blocks);
        foreach (var e in d.Events) e.Rule = Fix(e.Rule);
        foreach (var t in d.Tasks)
        {
            if (t.Rule != null) t.Rule = Fix(t.Rule);
            t.DoneOn ??= new();
        }
    }
}

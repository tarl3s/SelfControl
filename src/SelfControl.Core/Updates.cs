using System.Text.Json;

namespace SelfControl.Core;

/// <summary>A newer release found on GitHub.</summary>
public record UpdateInfo(string Version, string Notes, string ZipUrl = "");

public interface IUpdater
{
    /// <summary>The running version, "major.minor.patch".</summary>
    string CurrentVersion { get; }
    /// <summary>The newer release, or null (up to date, dev build, or no network). Never throws.</summary>
    Task<UpdateInfo?> CheckAsync();
    /// <summary>Downloads the release and starts the swap; the caller quits the app right after.</summary>
    Task InstallAsync(UpdateInfo update, IProgress<double>? progress = null);
}

/// <summary>Pure parts of the updater (no network, no processes).</summary>
public static class UpdateLogic
{
    public const string AssetName = "SelfControl-win-x64.zip";

    /// <summary>"v1.2.0" / "1.2" gives a 3-part version; null when it isn't one.</summary>
    public static Version? ParseVersion(string? tag)
    {
        var s = (tag ?? "").Trim().TrimStart('v', 'V');
        if (!Version.TryParse(s, out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    public static bool IsNewer(string? candidate, string? current)
    {
        var a = ParseVersion(candidate); var b = ParseVersion(current);
        return a != null && b != null && a > b;
    }

    /// <summary>Reads GitHub's "latest release" JSON; null without a version tag or the zip asset.</summary>
    public static UpdateInfo? ParseLatest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var v = ParseVersion(root.TryGetProperty("tag_name", out var t) ? t.GetString() : null);
            if (v == null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var n) || n.GetString() != AssetName) continue;
                if (!a.TryGetProperty("browser_download_url", out var u) || string.IsNullOrEmpty(u.GetString())) return null;
                var notes = root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
                return new UpdateInfo(v.ToString(), notes, u.GetString()!);
            }
            return null;
        }
        catch (Exception) { return null; }
    }
}

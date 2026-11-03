namespace SelfControl.Core;

public interface IBlocker
{
    event Action<string> AppSeen;
    event Action<string> AppClosed;
    void Kill(string exe);
    void SetWatched(IEnumerable<string> exes);
    /// <summary>Watched executables that are running right now.</summary>
    IReadOnlyCollection<string> Running => Array.Empty<string>();
}

public interface INotifier
{
    void Show(string title, string message);
}

public interface IAutostart
{
    bool Enabled { get; set; }
}

/// <summary>A program installed on the computer, as listed in Windows' "Installed apps".</summary>
public record InstalledApp(string Name, string? Publisher, IReadOnlyList<string> Exes, string? IconPng);

public interface IAppCatalog
{
    /// <summary>Installed programs, sorted by name. Slow (reads the registry and folders): call off the UI thread.</summary>
    IReadOnlyList<InstalledApp> Installed();
}

/// <summary>Whether someone is really using the computer.</summary>
public interface IActivity
{
    /// <summary>Time since the last key press or mouse move.</summary>
    TimeSpan IdleFor { get; }
    bool ScreenLocked { get; }
    /// <summary>Name of the program in front (e.g. "Steam"), or null.</summary>
    string? ForegroundApp => null;
    /// <summary>Site open in the browser in front (from the extension), or null.</summary>
    string? ActiveSite => null;
}

namespace SelfControl.Core;

public interface IBlocker
{
    event Action<string> AppSeen;
    event Action<string> AppClosed;
    void Kill(string exe);
    void SetWatched(IEnumerable<string> exes);
}

public interface INotifier
{
    void Show(string title, string message);
}

public interface IAutostart
{
    bool Enabled { get; set; }
}

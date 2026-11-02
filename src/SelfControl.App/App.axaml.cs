using Avalonia.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace SelfControl.App;

public partial class App : Application
{
    /// <summary>Set by the host (Desktop) before the app starts.</summary>
    public static Func<AppState>? StateFactory { get; set; }
    /// <summary>The state built by <see cref="StateFactory"/> (created after Avalonia starts, since it uses the dispatcher).</summary>
    public static AppState? State { get; private set; }
    /// <summary>Raised once, right after the state exists (the host starts the extension bridge here).</summary>
    public static event Action<AppState>? StateReady;
    /// <summary>True when launched with --tray: start hidden.</summary>
    public static bool StartHidden { get; set; }
    /// <summary>Called when a second launch asks the running instance to show itself.</summary>
    public static void ShowMain() => (Current as App)?.Show();

    MainWindow? _window;
    IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && StateFactory != null)
        {
            var state = State = StateFactory();
            StateReady?.Invoke(state);
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _window = new MainWindow(state);
            _window.Icon = MakeIcon();
            desktop.MainWindow = _window;
            if (!StartHidden) _window.Show();
            SetupTray();
            var warnings = new WarningManager(state);
            state.BreakDue += Views.BreakWindows.ShowPrompt;
            desktop.Exit += (_, _) => warnings.Dispose();
            desktop.Exit += (_, _) => state.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }

    void SetupTray()
    {
        var menu = new NativeMenu();
        var open = new NativeMenuItem("Abrir");
        open.Click += (_, _) => Show();
        var exit = new NativeMenuItem("Sair");
        exit.Click += (_, _) => Quit();
        menu.Items.Add(open);
        menu.Items.Add(exit);
        var tray = new TrayIcon { ToolTipText = "SelfControl", Menu = menu, Icon = MakeIcon(), IsVisible = true };
        tray.Clicked += (_, _) => Show();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    /// <summary>The white brain on black (Assets/SelfControl.ico, drawn by tools/Make-Icon.ps1).</summary>
    static WindowIcon MakeIcon() => new(AssetLoader.Open(new Uri("avares://SelfControl.App/Assets/SelfControl.ico")));

    public void Show()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_window == null) return;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    void Quit()
    {
        if (_window != null) _window.AllowClose = true;
        State?.SaveNow();
        _desktop?.Shutdown();
    }
}

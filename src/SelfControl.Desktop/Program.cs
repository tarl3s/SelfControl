using System.Diagnostics;
using System.Net;
using Avalonia;
using SelfControl.App;
using SelfControl.Core;

namespace SelfControl.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke")) return Smoke();
        if (args.Contains("--list-apps")) return ListApps();

        using var mutex = new Mutex(true, @"Local\SelfControl.SingleInstance", out var first);
        if (!first)
        {
            try { using var ev = EventWaitHandle.OpenExisting(ShowEventName); ev.Set(); } catch { }
            return 0;
        }
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelfControl");
        var firstRun = !File.Exists(Path.Combine(folder, "data.json"));

        void Log(string kind, Exception? ex)
        {
            try { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "error.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{kind}] {ex}{Environment.NewLine}"); } catch { }
        }
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log("task", e.Exception); e.SetObserved(); };

        using var blocker = new WindowsBlocker();
        ExtensionBridge? bridge = null;
        SelfControl.App.App.StartHidden = args.Contains("--tray");
        // AppState uses the Avalonia dispatcher, so it is built once Avalonia is running.
        SelfControl.App.App.StateFactory = () =>
            new AppState(new DataStore(folder), blocker, new ToastNotifier(), new RegistryAutostart(), firstRun)
            { Apps = new WindowsAppCatalog(Path.Combine(folder, "icons")), Updater = new Updater() };
        SelfControl.App.App.StateReady += state =>
        {
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) => { Log("ui", e.Exception); e.Handled = true; };
            bridge = new ExtensionBridge(
                h => { var i = state.Check(h); return new BridgeStatus(i.Blocked, i.Phase, i.Tasks, i.Remaining); },
                state.StartBreak, state.SiteClosed);
            try { bridge.Start(); } catch (Exception e) { Console.Error.WriteLine("Bridge not started: " + e.Message); }
        };

        new Thread(() =>
        {
            while (showEvent.WaitOne()) SelfControl.App.App.ShowMain();
        }) { IsBackground = true }.Start();

        try
        {
            return AppBuilder.Configure<SelfControl.App.App>().UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally { bridge?.Dispose(); }
    }

    const string ShowEventName = @"Local\SelfControl.Show";

    /// <summary>Dev check: prints the installed programs the block-list picker would show.</summary>
    static int ListApps()
    {
        var apps = new WindowsAppCatalog(Path.Combine(Path.GetTempPath(), "selfcontrol-icons")).Installed();
        foreach (var a in apps) Console.WriteLine($"{a.Name} | {a.Publisher} | {string.Join(", ", a.Exes)} | {(a.IconPng != null ? "icon" : "-")}");
        Console.WriteLine($"{apps.Count} programs");
        return 0;
    }

    static int Smoke()
    {
        try
        {
            using var blocker = new WindowsBlocker();
            var seen = new ManualResetEventSlim(); var closed = new ManualResetEventSlim();
            blocker.AppSeen += e => { Console.WriteLine("AppSeen " + e); seen.Set(); };
            blocker.AppClosed += e => { Console.WriteLine("AppClosed " + e); closed.Set(); };
            blocker.SetWatched(new[] { "notepad.exe" });
            blocker.Kill("notepad.exe"); // start clean
            Thread.Sleep(2500);
            seen.Reset(); closed.Reset();
            Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
            if (!seen.Wait(15000)) return Fail("AppSeen not raised");
            blocker.Kill("notepad.exe");
            if (!closed.Wait(15000)) return Fail("AppClosed not raised after Kill");

            new ToastNotifier().Show("SelfControl", "Smoke test");

            using var bridge = new ExtensionBridge(
                h => new BridgeStatus(true, "Warning", new[] { "tarefa" }, 0), (s, m) => { }, s => { });
            bridge.Start();
            using var http = new HttpClient();
            var rq = new HttpRequestMessage(HttpMethod.Get, ExtensionBridge.Prefix + "check?host=youtube.com");
            rq.Headers.Add("Origin", "chrome-extension://abc");
            var body = http.Send(rq).Content.ReadAsStringAsync().Result;
            Console.WriteLine(body);
            if (!body.Contains("\"blocked\":true")) return Fail("bad /check body");
            var ok = http.GetAsync(ExtensionBridge.Prefix + "check?host=x").Result;
            if (ok.StatusCode != HttpStatusCode.OK) return Fail("no-Origin request refused");
            var ev = new HttpRequestMessage(HttpMethod.Get, ExtensionBridge.Prefix + "check?host=x"); ev.Headers.Add("Origin", "http://evil.com");
            var bad = http.Send(ev);
            if (bad.StatusCode != HttpStatusCode.Forbidden) return Fail("evil origin not refused");
            Console.WriteLine("SMOKE OK");
            return 0;
        }
        catch (Exception ex) { return Fail(ex.ToString()); }
    }

    static int Fail(string m) { Console.WriteLine("SMOKE FAIL: " + m); return 1; }
}

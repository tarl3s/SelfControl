using System.Diagnostics;
using System.Net;

namespace SelfControl.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke")) return Smoke();
        // TODO(Task 6): real app startup (Avalonia, AppState wiring, tray) goes here.
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
            var bad = http.GetAsync(ExtensionBridge.Prefix + "check?host=x").Result;
            if (bad.StatusCode != HttpStatusCode.Forbidden) return Fail("no-origin request not refused");
            Console.WriteLine("SMOKE OK");
            return 0;
        }
        catch (Exception ex) { return Fail(ex.ToString()); }
    }

    static int Fail(string m) { Console.WriteLine("SMOKE FAIL: " + m); return 1; }
}

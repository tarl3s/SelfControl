using System.Net;
using System.Text;
using System.Text.Json;

namespace SelfControl.Desktop;

public record BridgeStatus(bool Blocked, string Phase, IReadOnlyList<string> Tasks, int Remaining);

/// <summary>Localhost HTTP bridge for the browser extension. Holds no lock logic.
/// THREADING: requests are served on a background thread, so the delegates passed in are called from it;
/// the app must lock/marshal inside them (BreaktimeTracker is not thread-safe).</summary>
public sealed class ExtensionBridge : IDisposable
{
    public const string Prefix = "http://127.0.0.1:47613/";
    readonly Func<string, BridgeStatus> _check;
    readonly Action<string, int> _startBreak;
    readonly Action<string> _closed;
    readonly HttpListener _http = new();

    public ExtensionBridge(Func<string, BridgeStatus> check, Action<string, int> startBreak, Action<string> closed)
    {
        _check = check; _startBreak = startBreak; _closed = closed;
        _http.Prefixes.Add(Prefix);
    }

    public void Start()
    {
        _http.Start();
        _ = Task.Run(Loop);
    }

    async Task Loop()
    {
        while (_http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _http.GetContextAsync(); }
            catch { return; }
            try { Handle(ctx); }
            catch { try { ctx.Response.StatusCode = 500; } catch { } }
            finally { try { ctx.Response.Close(); } catch { } }
        }
    }

    static string BareHost(string h)
    {
        h = (h ?? "").Trim();
        var i = h.IndexOf(':');
        return i >= 0 ? h[..i] : h;
    }

    void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request; var res = ctx.Response;
        var origin = req.Headers["Origin"];
        if (origin is null || !origin.StartsWith("chrome-extension://", StringComparison.Ordinal))
        { res.StatusCode = 403; return; }
        res.Headers["Access-Control-Allow-Origin"] = origin;
        res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; return; }

        var path = req.Url!.AbsolutePath;
        var q = req.QueryString;
        if (req.HttpMethod == "GET" && path == "/check")
        {
            var s = _check(BareHost(q["host"] ?? ""));
            var json = JsonSerializer.Serialize(new { blocked = s.Blocked, phase = s.Phase, tasks = s.Tasks, remaining = s.Remaining });
            var bytes = Encoding.UTF8.GetBytes(json);
            res.ContentType = "application/json; charset=utf-8";
            res.OutputStream.Write(bytes, 0, bytes.Length);
        }
        else if (req.HttpMethod == "POST" && path == "/break")
        {
            if (int.TryParse(q["min"], out var min) && !string.IsNullOrWhiteSpace(q["site"])) _startBreak(q["site"]!, min);
            else res.StatusCode = 400;
        }
        else if (req.HttpMethod == "POST" && path == "/closed")
        {
            if (!string.IsNullOrWhiteSpace(q["site"])) _closed(q["site"]!); else res.StatusCode = 400;
        }
        else res.StatusCode = 404;
    }

    public void Dispose()
    {
        try { _http.Close(); } catch { }
    }
}

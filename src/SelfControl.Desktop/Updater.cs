using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using SelfControl.App;
using SelfControl.Core;

namespace SelfControl.Desktop;

/// <summary>Updates SelfControl from the GitHub releases: download the zip, then a PowerShell script (run after the
/// app quits) copies the new SelfControl.exe and extension folder over the installed ones and starts it again.
/// A build run from a source checkout never updates itself.</summary>
sealed class Updater : IUpdater
{
    const string Repo = "tarl3s/SelfControl";

    public string CurrentVersion
    {
        get { var v = typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0); return new Version(v.Major, v.Minor, Math.Max(0, v.Build)).ToString(); }
    }

    static string InstallDir => AppContext.BaseDirectory.TrimEnd('\\', '/');
    static string WorkDir => Path.Combine(Path.GetTempPath(), "SelfControl_update");

    /// <summary>A SelfControl.sln or .csproj within 6 parent folders (this includes the owner's publish/ folder).</summary>
    public static bool IsDevBuild
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
                try { if (File.Exists(Path.Combine(d.FullName, "SelfControl.sln")) || d.EnumerateFiles("*.csproj").Any()) return true; } catch (Exception) { }
            return false;
        }
    }

    static HttpClient Http()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SelfControl-updater");
        return http;
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        if (IsDevBuild) return null;
        try
        {
            using var http = Http();
            var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            var r = UpdateLogic.ParseLatest(json);
            return r != null && UpdateLogic.IsNewer(r.Version, CurrentVersion) ? r : null;
        }
        catch (Exception) { return null; }
    }

    public async Task InstallAsync(UpdateInfo update, IProgress<double>? progress = null)
    {
        if (Directory.Exists(WorkDir)) try { Directory.Delete(WorkDir, true); } catch (Exception) { }
        Directory.CreateDirectory(WorkDir);
        var zip = Path.Combine(WorkDir, UpdateLogic.AssetName);
        using (var http = Http())
        {
            http.Timeout = TimeSpan.FromMinutes(15);
            using var resp = await http.GetAsync(update.ZipUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1, done = 0;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(zip);
            var buf = new byte[81920]; int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n)); done += n;
                if (total > 0) progress?.Report((double)done / total);
            }
        }
        var unpacked = Path.Combine(WorkDir, "new");
        ZipFile.ExtractToDirectory(zip, unpacked);
        var newDir = File.Exists(Path.Combine(unpacked, "SelfControl.exe")) ? unpacked
            : Directory.GetDirectories(unpacked).FirstOrDefault(d => File.Exists(Path.Combine(d, "SelfControl.exe")))
              ?? throw new InvalidDataException(L.T("The download doesn't contain SelfControl.exe."));
        StartSwap(newDir, InstallDir, Environment.ProcessId);
    }

    static void StartSwap(string newDir, string installDir, int waitForPid)
    {
        var script = Path.Combine(WorkDir, "swap.ps1");
        File.WriteAllText(script, SwapScript); // plain ASCII; paths and texts come in as arguments
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
                                  "-WaitPid", waitForPid.ToString(), "-NewDir", newDir, "-InstallDir", installDir,
                                  "-TxtUpdating", L.T("Updating SelfControl..."), "-TxtWaiting", L.T("Waiting for SelfControl to close..."),
                                  "-TxtInstalling", L.T("Installing the new version..."), "-TxtOpening", L.T("Opening SelfControl...") })
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    const string SwapScript = @"
param([int]$WaitPid, [string]$NewDir, [string]$InstallDir,
      [string]$TxtUpdating = 'Updating SelfControl...', [string]$TxtWaiting = 'Waiting for SelfControl to close...',
      [string]$TxtInstalling = 'Installing the new version...', [string]$TxtOpening = 'Opening SelfControl...')
$log = Join-Path $env:TEMP 'selfcontrol_update.log'
""$(Get-Date) updating $InstallDir from $NewDir"" | Out-File $log
$form = $null; $label = $null
try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'SelfControl'; $form.Width = 380; $form.Height = 130; $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'; $form.ControlBox = $false; $form.TopMost = $true
    $form.BackColor = [System.Drawing.Color]::Black
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $TxtUpdating; $label.AutoSize = $false; $label.Dock = 'Fill'
    $label.TextAlign = 'MiddleCenter'; $label.ForeColor = [System.Drawing.Color]::White
    $label.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $bar = New-Object System.Windows.Forms.ProgressBar
    $bar.Dock = 'Bottom'; $bar.Height = 14; $bar.Style = 'Marquee'
    $form.Controls.Add($label); $form.Controls.Add($bar); $form.Show(); [System.Windows.Forms.Application]::DoEvents()
} catch { $form = $null }
function Show-Step([string]$text) {
    if (-not $form) { return }
    if ($text) { $label.Text = $text }
    [System.Windows.Forms.Application]::DoEvents()
}
Show-Step $TxtWaiting
$deadline = (Get-Date).AddSeconds(60)
while ($WaitPid -gt 0 -and (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Show-Step ''; Start-Sleep -Milliseconds 100 }
Start-Sleep -Milliseconds 500
Show-Step $TxtInstalling
try {
    $src = (Resolve-Path -LiteralPath $NewDir).Path.TrimEnd('\')
    Copy-Item -LiteralPath (Join-Path $src 'SelfControl.exe') -Destination (Join-Path $InstallDir 'SelfControl.exe') -Force
    $ext = Join-Path $src 'extension'
    if (Test-Path -LiteralPath $ext -PathType Container) {
        $dest = Join-Path $InstallDir 'extension'
        if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue }
        Copy-Item -LiteralPath $ext -Destination $dest -Recurse -Force
    }
    ""$(Get-Date) copied"" | Out-File $log -Append
} catch { ""$(Get-Date) failed: $_"" | Out-File $log -Append }
Show-Step $TxtOpening
Start-Process (Join-Path $InstallDir 'SelfControl.exe') -WorkingDirectory $InstallDir
if ($form) { $form.Close() }
";
}

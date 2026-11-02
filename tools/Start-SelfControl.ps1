# Opens SelfControl, always at the latest version of the code in this folder.
# If any source file is newer than publish\SelfControl.exe, it republishes first (with a small "Atualizando..." window),
# then starts the exe. The Start menu shortcut runs this script:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<repo>\tools\Start-SelfControl.ps1"
# Saved as UTF-8 with BOM so Windows PowerShell 5.1 reads the accented texts correctly.
param([switch]$Force) # -Force: rebuild even if nothing looks changed

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'publish'
$exe = Join-Path $publish 'SelfControl.exe'
$project = Join-Path $root 'src\SelfControl.Desktop\SelfControl.Desktop.csproj'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

function Test-Stale {
    if ($Force -or -not (Test-Path $exe)) { return $true }
    $built = (Get-Item $exe).LastWriteTime
    $newer = Get-ChildItem (Join-Path $root 'src') -File -Recurse |
        Where-Object { $_.Extension -in '.cs', '.axaml', '.csproj', '.ico' -and $_.FullName -notmatch '\\(bin|obj)\\' -and $_.LastWriteTime -gt $built } |
        Select-Object -First 1
    return $null -ne $newer
}

function Get-Running { @(Get-Process SelfControl -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) }

trap {
    # anything unexpected: say so and leave a log, instead of silently not opening
    "$(Get-Date) $_`n$($_.ScriptStackTrace)" | Out-File (Join-Path $env:TEMP 'selfcontrol_start.log') -Append
    [System.Windows.Forms.MessageBox]::Show("Não foi possível abrir o SelfControl: $_`n`nLog: $env:TEMP\selfcontrol_start.log", 'SelfControl', 'OK', 'Error') | Out-Null
    exit 1
}

$stale = Test-Stale
if ($stale -and (Get-Running).Count -gt 0) {
    # Windows locks the exe while it runs; SelfControl saves every change right away, so closing it loses nothing
    $answer = [System.Windows.Forms.MessageBox]::Show(
        "Há uma versão nova do SelfControl.`n`nFechar o SelfControl aberto e atualizar agora?", 'SelfControl', 'YesNo', 'Information')
    if ($answer -eq 'Yes') {
        Get-Running | Stop-Process -Force
        Get-Running | ForEach-Object { $_.WaitForExit(5000) } | Out-Null
    }
    else { $stale = $false }
}

if ($stale) {
    # a small window while it builds, so a slow build doesn't look like nothing happened
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'SelfControl'; $form.Width = 360; $form.Height = 110; $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'; $form.ControlBox = $false; $form.TopMost = $true
    $form.BackColor = [System.Drawing.Color]::Black
    $label = New-Object System.Windows.Forms.Label
    $label.Text = 'Atualizando o SelfControl para a versão mais nova...'; $label.AutoSize = $false; $label.Dock = 'Fill'
    $label.TextAlign = 'MiddleCenter'; $label.ForeColor = [System.Drawing.Color]::White
    $label.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $form.Controls.Add($label); $form.Show(); [System.Windows.Forms.Application]::DoEvents()

    $log = Join-Path $env:TEMP 'selfcontrol_build.log'
    $p = Start-Process dotnet -ArgumentList 'publish', "`"$project`"", '-c', 'Release', '-r', 'win-x64', '--self-contained',
        '-p:PublishSingleFile=true', '-o', "`"$publish`"", '-nologo', '-v:q' `
        -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $log -PassThru
    $null = $p.Handle # PowerShell 5.1 only keeps the exit code if the handle was taken while the process ran
    while (-not $p.WaitForExit(100)) { [System.Windows.Forms.Application]::DoEvents() }
    $form.Close()

    if ($p.ExitCode -ne 0) {
        $errors = (Get-Content $log | Where-Object { $_ -match ' error ' } | Select-Object -First 5) -join "`n"
        [System.Windows.Forms.MessageBox]::Show("A atualização falhou, então a versão anterior vai abrir.`n`n$errors`n`nLog completo: $log", 'SelfControl', 'OK', 'Warning') | Out-Null
    }
}

if (Test-Path $exe) { Start-Process $exe -WorkingDirectory $publish }
else { [System.Windows.Forms.MessageBox]::Show("O SelfControl ainda não foi compilado e a compilação falhou. Veja $env:TEMP\selfcontrol_build.log", 'SelfControl', 'OK', 'Error') | Out-Null }

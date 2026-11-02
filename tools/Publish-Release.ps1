# Publishes the current version (<Version> in src/SelfControl.Desktop/SelfControl.Desktop.csproj) as a GitHub release:
# SelfControl-win-x64.zip = the single-file SelfControl.exe + the browser extension folder. The app's updater downloads it.
# The tag vX.Y.Z must already be pushed.   powershell -ExecutionPolicy Bypass -File tools\Publish-Release.ps1 -NotesFile notes.md
param([Parameter(Mandatory)][string]$NotesFile, [switch]$NoUpload)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gh = 'C:\Program Files\GitHub CLI\gh.exe'
$project = Join-Path $root 'src\SelfControl.Desktop\SelfControl.Desktop.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }
$work = Join-Path $env:TEMP 'SelfControl_publish'
$out = Join-Path $work 'SelfControl'
$zip = Join-Path $work 'SelfControl-win-x64.zip'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }

dotnet publish $project -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:DebugType=None -o $out -nologo -v:q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Copy-Item (Join-Path $root 'extension') (Join-Path $out 'extension') -Recurse
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
"built $zip ($([Math]::Round((Get-Item $zip).Length / 1MB, 1)) MB) for v$version"

if (-not $NoUpload) {
    & $gh release create "v$version" $zip --repo tarl3s/SelfControl --title "SelfControl v$version" --notes-file $NotesFile --verify-tag
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
}

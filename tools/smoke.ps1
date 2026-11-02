# Headless smoke test. WARNING: closes any open Notepad.
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project "$root/src/SelfControl.Desktop" -- --smoke
exit $LASTEXITCODE

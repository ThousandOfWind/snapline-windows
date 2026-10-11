param([string]$Executable)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $root 'artifacts/Snapline-win-x64/Snapline.exe' }
if (-not (Test-Path $Executable)) { throw 'Build the portable package with ./scripts/Build.ps1 first.' }
if (Get-Process Snapline -ErrorAction SilentlyContinue) { throw 'Exit Snapline via the tray before E2E. The test does not stop an existing app.' }
$report = Join-Path $root 'artifacts/e2e-report.json'
New-Item -ItemType Directory -Force (Split-Path $report -Parent) | Out-Null
Write-Host 'E2E drives real keyboard/mouse/UI Automation in the current interactive Windows session.'
Write-Host 'Use an idle unlocked desktop. Clipboard will be replaced with generated test data, never read beforehand.'
Write-Host 'No existing image folders are watched; region capture uses a synthetic image, not the actual desktop.'
& dotnet run --project (Join-Path $root 'tests/Snapline.E2E/Snapline.E2E.csproj') -c Release -- $Executable $report
if ($LASTEXITCODE -ne 0) { throw "E2E failed. See $report" }
Write-Host "E2E passed. Report: $report"

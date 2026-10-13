param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path $Executable).Path
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
if (-not ($fileVersion -eq $Version -or $fileVersion.StartsWith("$Version+", [StringComparison]::Ordinal))) { throw 'Published EXE version does not match tag.' }
# New isolated synthetic startup only. No clipboard, folder watcher, tray, global hooks, screenshot or physical input.
$process = Start-Process $exe -ArgumentList '--smoke-test' -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Release EXE synthetic startup timed out; no release should be published.' }
if ($process.ExitCode -ne 0) { throw "Release EXE synthetic startup failed: $($process.ExitCode)." }
$report = [ordered]@{
    status = 'pass'; exitCode = $process.ExitCode; version = $fileVersion
    executableSha256 = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    scope = 'synthetic process startup/shutdown only; not physical UI E2E'
    clipboardAccessed = $false; inputInjected = $false; actualDesktopCaptured = $false
}
New-Item -ItemType Directory -Force (Split-Path ([IO.Path]::GetFullPath($ReportPath)) -Parent) | Out-Null
$report | ConvertTo-Json -Depth 4 | Set-Content $ReportPath -Encoding utf8
Write-Host 'Release EXE version + synthetic startup passed.'

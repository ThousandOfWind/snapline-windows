param([string]$Executable)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Executable) {
    & dotnet build (Join-Path $root 'src/Snapline.Windows/Snapline.Windows.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $Executable = Join-Path $root 'src/Snapline.Windows/bin/Release/net10.0-windows/Snapline.exe'
}
$output = Join-Path $root ('artifacts/test-runs/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-motion')
New-Item -ItemType Directory -Force $output | Out-Null
$arguments = '--verify-motion "' + $output + '"'
$process = Start-Process $Executable -ArgumentList $arguments -PassThru
if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Motion verification timed out.' }
if ($process.ExitCode -ne 0) { throw "Motion verification failed. Report: $output/motion-report.json" }
Get-Content (Join-Path $output 'motion-report.json') -Raw | ConvertFrom-Json | Select-Object passed,failure,actualDesktopCaptured,clipboardAccessed,inputInjected | Format-List
Write-Host "Motion evidence: $output"

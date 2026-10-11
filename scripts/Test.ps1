$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $root 'tests/Snapline.Tests/Snapline.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
& dotnet build (Join-Path $root 'src/Snapline.Windows/Snapline.Windows.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$exe = Join-Path $root 'src/Snapline.Windows/bin/Release/net10.0-windows/Snapline.exe'
$process = Start-Process $exe -ArgumentList '--smoke-test' -PassThru
if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'Smoke test timed out.' }
if ($process.ExitCode -ne 0) { throw "UI smoke test failed: $($process.ExitCode)" }
Write-Host 'UI smoke test passed: synthetic image, window rendering, clean shutdown; no desktop/clipboard capture.'

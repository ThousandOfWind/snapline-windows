param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/Snapline.Windows/Snapline.Windows.csproj'
$output = if ($OutputDirectory) { [System.IO.Path]::GetFullPath((Join-Path $root $OutputDirectory)) } else { Join-Path $root "artifacts/Snapline-$Runtime" }
if (Get-Process Snapline -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $output 'Snapline.exe') }) {
    throw 'The target EXE is running. Exit via its tray or use a different -OutputDirectory; the build will not stop the application.'
}
& dotnet run --project (Join-Path $root 'tests/Snapline.Tests/Snapline.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& dotnet publish $project -c Release -r $Runtime --self-contained $selfContained -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $root 'README.md') $output
$zip = "$output.zip"
Compress-Archive -Path "$output/*" -DestinationPath $zip -Force
Write-Host "Portable application: $output/Snapline.exe"
Write-Host "ZIP: $zip"
Get-FileHash $zip -Algorithm SHA256 | Format-List

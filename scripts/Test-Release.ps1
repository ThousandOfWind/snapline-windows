$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content (Join-Path $root 'src/Snapline.Windows/Snapline.Windows.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$validate = Join-Path $PSScriptRoot 'Release-Validate.ps1'
$valid = & $validate -Tag "v$version"
if ($valid.version -ne $version -or $valid.gitTagVerified) { throw 'Version-only validation returned unexpected state.' }
$count = 1
foreach ($tag in @('main', 'v01.0.0', 'v1.2', 'v1.2.3/other', 'v1.2.3;echo-test', 'v999.999.999')) {
    $rejected = $false
    try { & $validate -Tag $tag | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid/mismatched release tag accepted: $tag" }
    $count++
}
Write-Host "$count release-validation checks passed. No EXE launched, no GitHub publication performed."

param(
    [Parameter(Mandatory)][string]$Tag,
    [switch]$RequireGitTag
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Tag -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw 'Tag must be vMAJOR.MINOR.PATCH (optional prerelease suffix).' }
$version = $Tag.Substring(1)
[xml]$project = Get-Content (Join-Path $root 'src/Snapline.Windows/Snapline.Windows.csproj') -Raw
$declared = [string]$project.Project.PropertyGroup.Version
if ($declared -ne $version) { throw "Tag version $version does not match project Version $declared." }
Push-Location $root
try {
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read source commit.' }
    if ($RequireGitTag) {
        $tagCommit = (& git rev-parse --verify "refs/tags/$Tag^{commit}" 2>$null).Trim()
        if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $commit) { throw 'Checkout is not the requested tag commit.' }
        & git merge-base --is-ancestor $commit refs/remotes/origin/main
        if ($LASTEXITCODE -ne 0) { throw 'Release tag must point to a commit already merged into origin/main.' }
        if (& git status --porcelain --untracked-files=no) { throw 'Release checkout must be clean.' }
    }
    [pscustomobject]@{ tag = $Tag; version = $version; commit = $commit; prerelease = $version.Contains('-'); gitTagVerified = [bool]$RequireGitTag }
} finally { Pop-Location }

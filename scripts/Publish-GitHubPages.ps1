param(
    [string]$SourceDir = "artifacts/manifest",
    [string]$Branch = "gh-pages",
    [string]$Remote = "origin"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourcePath = Resolve-Path (Join-Path $repoRoot $SourceDir)
$worktreePath = Join-Path ([System.IO.Path]::GetTempPath()) "watchlistrequestsync-gh-pages"

if (Test-Path $worktreePath) {
    Remove-Item $worktreePath -Recurse -Force
}

git worktree prune | Out-Null

$remoteExists = $false
git ls-remote --exit-code --heads $Remote $Branch *> $null
if ($LASTEXITCODE -eq 0) {
    $remoteExists = $true
}

if ($remoteExists) {
    git worktree add $worktreePath "$Remote/$Branch" | Out-Null
}
else {
    git worktree add --detach $worktreePath HEAD | Out-Null
    Push-Location $worktreePath
    git checkout --orphan $Branch | Out-Null
    Get-ChildItem -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force
    Pop-Location
}

Push-Location $worktreePath

Get-ChildItem -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force
Copy-Item (Join-Path $sourcePath "*") -Destination $worktreePath -Recurse -Force

git add .
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git config user.name "github-actions[bot]"
    git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
    git commit -m "Update Jellyfin plugin manifest" | Out-Null
    git push $Remote "HEAD:$Branch"
}
else {
    Write-Host "No gh-pages changes to publish."
}

Pop-Location
git worktree remove $worktreePath --force | Out-Null

param(
    [Parameter(Mandatory = $true)]
    [string]$Owner,
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    [string]$OutputPath = "artifacts/manifest/manifest.json",
    [string]$DefaultBranch = "main"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$currentMetadata = & (Join-Path $PSScriptRoot "Get-BuildMetadata.ps1")
$outputFile = Join-Path $repoRoot $OutputPath
$outputDir = Split-Path $outputFile -Parent
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$headers = @{
    "User-Agent" = "WatchlistRequestsSync-manifest-generator"
    "Accept" = "application/vnd.github+json"
}

if ($env:GITHUB_TOKEN) {
    $headers["Authorization"] = "Bearer $($env:GITHUB_TOKEN)"
}

$releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Owner/$Repository/releases?per_page=100" -Headers $headers
$stableReleases = @($releases | Where-Object { -not $_.draft -and -not $_.prerelease })

$versionEntries = foreach ($release in $stableReleases) {
    $tag = $release.tag_name
    $buildYamlUrl = "https://raw.githubusercontent.com/$Owner/$Repository/$tag/build.yaml"

    try {
        $tempBuildYaml = Join-Path ([System.IO.Path]::GetTempPath()) ("build-{0}.yaml" -f ([Guid]::NewGuid().ToString("N")))
        Set-Content -Path $tempBuildYaml -Value (Invoke-RestMethod -Uri $buildYamlUrl -Headers $headers)
        $releaseMetadata = & (Join-Path $PSScriptRoot "Get-BuildMetadata.ps1") -Path $tempBuildYaml
    }
    catch {
        Write-Warning "Skipping release '$tag' because build.yaml could not be loaded."
        continue
    }
    finally {
        if ($tempBuildYaml -and (Test-Path $tempBuildYaml)) {
            Remove-Item $tempBuildYaml -Force
        }
    }

    $packageBaseName = [System.IO.Path]::GetFileNameWithoutExtension(($releaseMetadata.artifacts | Where-Object { $_ -like "*.dll" } | Select-Object -First 1))
    $expectedZipName = "{0}_{1}.zip" -f $packageBaseName, $releaseMetadata.version
    $zipAsset = @($release.assets | Where-Object { $_.name -eq $expectedZipName }) | Select-Object -First 1
    if (-not $zipAsset) {
        Write-Warning "Skipping release '$tag' because zip asset '$expectedZipName' was not found."
        continue
    }

    $checksumAsset = @($release.assets | Where-Object { $_.name -eq "$expectedZipName.md5" }) | Select-Object -First 1
    if (-not $checksumAsset) {
        Write-Warning "Skipping release '$tag' because checksum asset '$expectedZipName.md5' was not found."
        continue
    }

    $checksum = (Invoke-RestMethod -Uri $checksumAsset.browser_download_url -Headers $headers).ToString().Trim().Split(" ")[0].ToLowerInvariant()
    $changelog = if ([string]::IsNullOrWhiteSpace($release.body)) { $releaseMetadata.changelog } else { $release.body }

    [ordered]@{
        version = $releaseMetadata.version
        changelog = $changelog
        targetAbi = $releaseMetadata.targetAbi
        sourceUrl = $zipAsset.browser_download_url
        checksum = $checksum
        timestamp = ([DateTimeOffset]$release.published_at).ToUniversalTime().ToString("O")
    }
}

$versionEntries = @($versionEntries | Sort-Object { [DateTimeOffset]$_.timestamp } -Descending)

$pluginManifest = @(
    [ordered]@{
        guid = $currentMetadata.guid
        name = $currentMetadata.name
        overview = $currentMetadata.overview
        description = $currentMetadata.description
        owner = $currentMetadata.owner
        category = $currentMetadata.category
        imageUrl = $currentMetadata.imageUrl
        versions = $versionEntries
    }
)

$pluginManifest | ConvertTo-Json -Depth 10 -AsArray | Set-Content -Path $outputFile
$pluginManifest | ConvertTo-Json -Depth 10 -AsArray

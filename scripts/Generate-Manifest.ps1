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

function Get-ReleaseManifestEntry {
    param(
        [Parameter(Mandatory = $true)]
        $Release,
        [Parameter(Mandatory = $true)]
        [string]$Owner,
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [hashtable]$Headers,
        [Parameter(Mandatory = $true)]
        [string]$ScriptRoot
    )

    $maxAttempts = 6
    $delaySeconds = 5
    $releaseApiUrl = $Release.url

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $currentRelease = if ($attempt -eq 1) { $Release } else { Invoke-RestMethod -Uri $releaseApiUrl -Headers $Headers }
        $tag = $currentRelease.tag_name
        $buildYamlUrl = "https://raw.githubusercontent.com/$Owner/$Repository/$tag/build.yaml"

        try {
            $tempBuildYaml = Join-Path ([System.IO.Path]::GetTempPath()) ("build-{0}.yaml" -f ([Guid]::NewGuid().ToString("N")))
            Set-Content -Path $tempBuildYaml -Value (Invoke-RestMethod -Uri $buildYamlUrl -Headers $Headers)
            $releaseMetadata = & (Join-Path $ScriptRoot "Get-BuildMetadata.ps1") -Path $tempBuildYaml
        }
        catch {
            if ($attempt -lt $maxAttempts) {
                Write-Warning "Release '$tag' metadata was not yet available (attempt $attempt/$maxAttempts). Retrying in $delaySeconds seconds."
                Start-Sleep -Seconds $delaySeconds
                continue
            }

            Write-Warning "Skipping release '$tag' because build.yaml could not be loaded."
            return $null
        }
        finally {
            if ($tempBuildYaml -and (Test-Path $tempBuildYaml)) {
                Remove-Item $tempBuildYaml -Force
            }
        }

        $packageBaseName = [System.IO.Path]::GetFileNameWithoutExtension(($releaseMetadata.artifacts | Where-Object { $_ -like "*.dll" } | Select-Object -First 1))
        $expectedZipName = "{0}_{1}.zip" -f $packageBaseName, $releaseMetadata.version
        $zipAsset = @($currentRelease.assets | Where-Object { $_.name -eq $expectedZipName }) | Select-Object -First 1
        $checksumAsset = @($currentRelease.assets | Where-Object { $_.name -eq "$expectedZipName.md5" }) | Select-Object -First 1

        if (-not $zipAsset -or -not $checksumAsset) {
            if ($attempt -lt $maxAttempts) {
                Write-Warning "Release '$tag' assets were not yet visible to the API (attempt $attempt/$maxAttempts). Retrying in $delaySeconds seconds."
                Start-Sleep -Seconds $delaySeconds
                continue
            }

            if (-not $zipAsset) {
                Write-Warning "Skipping release '$tag' because zip asset '$expectedZipName' was not found."
            }

            if (-not $checksumAsset) {
                Write-Warning "Skipping release '$tag' because checksum asset '$expectedZipName.md5' was not found."
            }

            return $null
        }

        $checksum = (Invoke-RestMethod -Uri $checksumAsset.browser_download_url -Headers $Headers).ToString().Trim().Split(" ")[0].ToLowerInvariant()
        $changelog = if ([string]::IsNullOrWhiteSpace($currentRelease.body)) { $releaseMetadata.changelog } else { $currentRelease.body }

        return [ordered]@{
            version = $releaseMetadata.version
            changelog = $changelog
            targetAbi = $releaseMetadata.targetAbi
            sourceUrl = $zipAsset.browser_download_url
            checksum = $checksum
            timestamp = ([DateTimeOffset]$currentRelease.published_at).ToUniversalTime().ToString("O")
        }
    }

    return $null
}

$releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Owner/$Repository/releases?per_page=100" -Headers $headers
$stableReleases = @($releases | Where-Object { -not $_.draft -and -not $_.prerelease })

$versionEntries = foreach ($release in $stableReleases) {
    Get-ReleaseManifestEntry -Release $release -Owner $Owner -Repository $Repository -Headers $headers -ScriptRoot $PSScriptRoot
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

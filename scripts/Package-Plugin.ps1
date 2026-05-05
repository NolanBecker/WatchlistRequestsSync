param(
    [string]$ProjectPath = "src/Jellyfin.Plugin.WatchlistRequestsSync/Jellyfin.Plugin.WatchlistRequestsSync.csproj",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/package"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$metadata = & (Join-Path $PSScriptRoot "Get-BuildMetadata.ps1")
$project = Resolve-Path (Join-Path $repoRoot $ProjectPath)
$outputRootPath = Join-Path $repoRoot $OutputRoot
$publishDir = Join-Path $outputRootPath "publish"
$stageDir = Join-Path $outputRootPath "staging"
$assetName = "{0}_{1}.zip" -f $metadata.PackageBaseName, $metadata.version
$zipPath = Join-Path $outputRootPath $assetName
$checksumPath = "$zipPath.md5"
$manifestPath = Join-Path $outputRootPath "package-metadata.json"

if (Test-Path $outputRootPath) {
    Remove-Item $outputRootPath -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir | Out-Null
New-Item -ItemType Directory -Path $stageDir | Out-Null

$publishArgs = @(
    "publish", $project.Path,
    "-c", $Configuration,
    "-o", $publishDir,
    "/p:Version=$($metadata.version)",
    "/p:AssemblyVersion=$($metadata.version)",
    "/p:FileVersion=$($metadata.version)",
    "/p:InformationalVersion=$($metadata.version)"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

foreach ($artifact in $metadata.artifacts) {
    $artifactPath = Join-Path $publishDir $artifact
    if (-not (Test-Path $artifactPath)) {
        throw "Expected packaged artifact '$artifact' was not found in publish output."
    }

    Copy-Item $artifactPath -Destination (Join-Path $stageDir $artifact)
}

Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$checksum = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLowerInvariant()
Set-Content -Path $checksumPath -Value $checksum -NoNewline

$packageMetadata = [ordered]@{
    version = $metadata.version
    targetAbi = $metadata.targetAbi
    framework = $metadata.framework
    assetName = $assetName
    zipPath = $zipPath
    checksum = $checksum
    checksumPath = $checksumPath
    stagedArtifacts = @($metadata.artifacts)
}

$packageMetadata | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath
$packageMetadata | ConvertTo-Json -Depth 5

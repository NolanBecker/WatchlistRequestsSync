param(
    [string]$Path = (Join-Path $PSScriptRoot "..\build.yaml")
)

$resolvedPath = Resolve-Path $Path
$lines = Get-Content $resolvedPath

$metadata = [ordered]@{}
$currentKey = $null
$currentBlock = $null

foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    if ($line -match '^\s*-\s+"?(.*?)"?\s*$') {
        if (-not $currentKey) {
            throw "Encountered list item before a list key in build metadata."
        }

        if (-not $metadata.Contains($currentKey)) {
            $metadata[$currentKey] = @()
        }

        $metadata[$currentKey] += $matches[1]
        continue
    }

    if ($currentBlock -and $line -match '^\s+(.+?)\s*$') {
        $metadata[$currentBlock] = (($metadata[$currentBlock], $matches[1]) -join " ").Trim()
        continue
    }

    $currentBlock = $null
    $currentKey = $null

    if ($line -match '^\s*([A-Za-z0-9_]+):\s*>\s*$') {
        $currentBlock = $matches[1]
        $metadata[$currentBlock] = ""
        continue
    }

    if ($line -match '^\s*([A-Za-z0-9_]+):\s*$') {
        $currentKey = $matches[1]
        $metadata[$currentKey] = @()
        continue
    }

    if ($line -match '^\s*([A-Za-z0-9_]+):\s*"?(.*?)"?\s*$') {
        $metadata[$matches[1]] = $matches[2]
        continue
    }
}

if (-not $metadata.Contains("artifacts") -or @($metadata["artifacts"]).Count -eq 0) {
    throw "build.yaml must define at least one artifact."
}

$primaryAssembly = @($metadata["artifacts"] | Where-Object { $_ -like "*.dll" }) | Select-Object -First 1
if (-not $primaryAssembly) {
    throw "build.yaml must contain a primary DLL artifact."
}

$result = [pscustomobject]$metadata
$result | Add-Member -NotePropertyName BuildMetadataPath -NotePropertyValue $resolvedPath.Path -Force
$result | Add-Member -NotePropertyName PrimaryAssembly -NotePropertyValue $primaryAssembly -Force
$result | Add-Member -NotePropertyName PackageBaseName -NotePropertyValue ([System.IO.Path]::GetFileNameWithoutExtension($primaryAssembly)) -Force

$result

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReleaseDirectory
)

$ErrorActionPreference = 'Stop'
$releaseRoot = [System.IO.Path]::GetFullPath($ReleaseDirectory)
$checksumPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $checksumPath)) { throw "Missing checksum file: $checksumPath" }

$failures = 0
$verifiedFiles = @{}
function Add-Failure([string]$Message) {
    Write-Host "ERROR  $Message" -ForegroundColor Red
    $script:failures++
}

foreach ($line in Get-Content -LiteralPath $checksumPath) {
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$') {
        Add-Failure "Invalid checksum line: $line"
        continue
    }

    $expected = $Matches[1].ToLowerInvariant()
    $fileName = $Matches[2]
    $filePath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $fileName))
    if (-not $filePath.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Failure "Checksum target escapes release directory: $fileName"
        continue
    }
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        Add-Failure "Missing release file: $fileName"
        continue
    }

    $actual = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        Add-Failure "Checksum mismatch: $fileName"
    } else {
        $verifiedFiles[$fileName] = $actual
        Write-Host "OK  $fileName"
    }
}

if ($verifiedFiles.Count -eq 0) { Add-Failure 'The checksum file contains no valid release downloads.' }
if (-not ($verifiedFiles.Keys | Where-Object { $_ -like '*.exe' })) { Add-Failure 'The checksum file does not include an installer.' }
if (-not ($verifiedFiles.Keys | Where-Object { $_ -like '*.zip' })) { Add-Failure 'The checksum file does not include a portable archive.' }

$manifestPath = Join-Path $releaseRoot 'release-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Add-Failure 'Missing release manifest: release-manifest.json'
} else {
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifestFiles = @($manifest.files)
        if ($manifestFiles.Count -eq 0) { Add-Failure 'The release manifest contains no files.' }

        foreach ($entry in $manifestFiles) {
            $entryName = [string]$entry.name
            if (-not $verifiedFiles.ContainsKey($entryName)) {
                Add-Failure "Manifest file is absent from the verified checksum list: $entryName"
                continue
            }

            $entryPath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $entryName))
            if (-not $entryPath.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-Failure "Manifest target escapes the release directory: $entryName"
                continue
            }
            $entryInfo = Get-Item -LiteralPath $entryPath
            if ([long]$entry.bytes -ne $entryInfo.Length) { Add-Failure "Manifest size mismatch: $entryName" }
            if ([string]$entry.sha256 -ne $verifiedFiles[$entryName]) { Add-Failure "Manifest checksum mismatch: $entryName" }
        }

        foreach ($verifiedName in $verifiedFiles.Keys) {
            if (-not ($manifestFiles | Where-Object { [string]$_.name -eq $verifiedName })) {
                Add-Failure "Verified download is absent from the manifest: $verifiedName"
            }
        }
    } catch {
        Add-Failure "Invalid release manifest: $($_.Exception.Message)"
    }
}

if ($failures -gt 0) { throw "$failures release verification check(s) failed." }
Write-Host 'All release checksums and manifest entries are valid.'

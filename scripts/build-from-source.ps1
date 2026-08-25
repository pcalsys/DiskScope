[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('Build', 'Install', 'Uninstall')]
    [string]$Action = 'Build',

    [Parameter()]
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$OpenOutput,

    [Parameter()]
    [switch]$NoDesktopShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ValidatedChildPath {
    param(
        [Parameter(Mandatory)]
        [string]$Parent,

        [Parameter(Mandatory)]
        [string]$Child
    )

    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $resolvedChild = [System.IO.Path]::GetFullPath($Child)
    $requiredPrefix = $resolvedParent + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedChild.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved path is outside its expected parent: $resolvedChild"
    }

    return $resolvedChild
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to modify a generated or installed path through a reparse point: $Path"
        }
    }
}

function Get-SourceInstallLocations {
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $roamingAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    $desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    if ([string]::IsNullOrWhiteSpace($localAppData) -or [string]::IsNullOrWhiteSpace($roamingAppData)) {
        throw 'Windows user application-data folders could not be resolved.'
    }

    $programsRoot = [System.IO.Path]::GetFullPath((Join-Path $localAppData 'Programs'))
    $installRoot = Get-ValidatedChildPath -Parent $programsRoot -Child (Join-Path $programsRoot 'DiskScope Source Build')
    $startMenuPrograms = [System.IO.Path]::GetFullPath((Join-Path $roamingAppData 'Microsoft\Windows\Start Menu\Programs'))
    $startMenuRoot = Get-ValidatedChildPath -Parent $startMenuPrograms -Child (Join-Path $startMenuPrograms 'DiskScope Source Build')
    $desktopShortcut = if ([string]::IsNullOrWhiteSpace($desktopDirectory)) {
        $null
    } else {
        Join-Path $desktopDirectory 'DiskScope (source build).lnk'
    }

    return [pscustomobject]@{
        InstallRoot = $installRoot
        MarkerPath = Join-Path $installRoot '.diskscope-source-install.json'
        InstalledExecutable = Join-Path $installRoot 'DiskScope.exe'
        StartMenuRoot = $startMenuRoot
        StartMenuShortcut = Join-Path $startMenuRoot 'DiskScope (source build).lnk'
        DesktopShortcut = $desktopShortcut
    }
}

function Remove-SourceInstall {
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Locations
    )

    Assert-NotReparsePoint -Path $Locations.InstallRoot
    if (-not (Test-Path -LiteralPath $Locations.MarkerPath -PathType Leaf)) {
        Write-Host 'No DiskScope source-build installation was found. Nothing was changed.'
        return
    }

    $marker = Get-Content -LiteralPath $Locations.MarkerPath -Raw | ConvertFrom-Json
    if ($marker.product -ne 'DiskScope' -or $marker.installType -ne 'source-build') {
        throw 'The installation marker is invalid. No files were removed.'
    }

    foreach ($shortcutPath in @($Locations.StartMenuShortcut, $Locations.DesktopShortcut)) {
        if (-not [string]::IsNullOrWhiteSpace($shortcutPath) -and (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
            Remove-Item -LiteralPath $shortcutPath -Force
        }
    }

    if (Test-Path -LiteralPath $Locations.InstalledExecutable -PathType Leaf) {
        Remove-Item -LiteralPath $Locations.InstalledExecutable -Force
    }
    Remove-Item -LiteralPath $Locations.MarkerPath -Force

    if ((Test-Path -LiteralPath $Locations.StartMenuRoot -PathType Container) -and
        @(Get-ChildItem -LiteralPath $Locations.StartMenuRoot -Force).Count -eq 0) {
        Remove-Item -LiteralPath $Locations.StartMenuRoot -Force
    }
    if ((Test-Path -LiteralPath $Locations.InstallRoot -PathType Container) -and
        @(Get-ChildItem -LiteralPath $Locations.InstallRoot -Force).Count -eq 0) {
        Remove-Item -LiteralPath $Locations.InstallRoot -Force
    }

    Write-Host 'DiskScope source-build installation removed.' -ForegroundColor Green
}

function New-WindowsShortcut {
    param(
        [Parameter(Mandatory)]
        [string]$ShortcutPath,

        [Parameter(Mandatory)]
        [string]$TargetPath,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    $shortcutParent = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Path $shortcutParent -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = "$TargetPath,0"
        $shortcut.Description = 'DiskScope built locally from public source code'
        $shortcut.Save()
    } finally {
        if ($null -ne $shell) {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        }
    }
}

$sourceInstallLocations = Get-SourceInstallLocations
if ($Action -eq 'Uninstall') {
    Remove-SourceInstall -Locations $sourceInstallLocations
    return
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repoRoot 'DiskScope.sln'
$projectPath = Join-Path $repoRoot 'src\DiskScope\DiskScope.csproj'
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'Run this script from a complete DiskScope source checkout or source archive.'
}

$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "The project version is missing or invalid: $version"
}

$artifactBase = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\source-build'))
$artifactRoot = Get-ValidatedChildPath -Parent $artifactBase -Child (Join-Path $artifactBase $version)
$publishRoot = Get-ValidatedChildPath -Parent $artifactRoot -Child (Join-Path $artifactRoot "DiskScope-$version-$Runtime")
$portableZip = Join-Path $artifactRoot "DiskScope-$version-$Runtime-portable.zip"
$checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
$manifestPath = Join-Path $artifactRoot 'source-build-manifest.json'

$dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
$dotnet = if ($dotnetCommand) {
    $dotnetCommand.Source
} else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw 'The .NET 8 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0'
}

$dotnetSdkVersion = [string](& $dotnet --version)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdkVersion)) {
    throw 'The .NET SDK could not be started. Check global.json and the installed SDK version.'
}
$dotnetSdkVersion = $dotnetSdkVersion.Trim()

$artifactsRoot = Join-Path $repoRoot 'artifacts'
foreach ($generatedPath in @($artifactsRoot, $artifactBase, $artifactRoot)) {
    Assert-NotReparsePoint -Path $generatedPath
}
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Write-Host "Building DiskScope $version from source with .NET SDK $dotnetSdkVersion..." -ForegroundColor Cyan
& $dotnet restore $solutionPath --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& $dotnet restore $projectPath --runtime $Runtime
if ($LASTEXITCODE -ne 0) { throw 'Runtime-specific dotnet restore failed.' }

& $dotnet build $solutionPath -c Release --no-restore --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

if (-not $SkipTests) {
    & $dotnet test $solutionPath -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
}

& $dotnet publish $projectPath `
    -c Release -r $Runtime --self-contained true --no-restore `
    -o $publishRoot `
    -p:Version=$version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$publishedExecutable = Join-Path $publishRoot 'DiskScope.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw 'The locally published DiskScope executable is missing.'
}

$privateBuildRoots = @($repoRoot, $env:USERPROFILE) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd('\') } |
    Sort-Object -Unique
$publishedBytes = [System.IO.File]::ReadAllBytes($publishedExecutable)
$publishedAsciiText = [System.Text.Encoding]::ASCII.GetString($publishedBytes)
$publishedUnicodeText = [System.Text.Encoding]::Unicode.GetString($publishedBytes)
foreach ($privateBuildRoot in $privateBuildRoots) {
    $containsAsciiPath = $publishedAsciiText.IndexOf(
        $privateBuildRoot,
        [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    $containsUnicodePath = $publishedUnicodeText.IndexOf(
        $privateBuildRoot,
        [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    if ($containsAsciiPath -or $containsUnicodePath) {
        throw 'The locally published executable contains a machine-local build path.'
    }
}

Compress-Archive -LiteralPath $publishedExecutable -DestinationPath $portableZip -CompressionLevel Optimal
$executableHash = (Get-FileHash -LiteralPath $publishedExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$zipHash = (Get-FileHash -LiteralPath $portableZip -Algorithm SHA256).Hash.ToLowerInvariant()
@(
    "$executableHash  DiskScope-$version-$Runtime/DiskScope.exe",
    "$zipHash  DiskScope-$version-$Runtime-portable.zip"
) | Set-Content -LiteralPath $checksumPath -Encoding ascii

$sourceCommit = 'source-archive'
$sourceState = 'source-archive'
$gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gitCommand -and (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) {
    $commitCandidate = [string](& $gitCommand.Source -C $repoRoot rev-parse HEAD)
    if ($LASTEXITCODE -eq 0 -and $commitCandidate -match '^[0-9a-f]{40}$') {
        $sourceCommit = $commitCandidate
        $sourceChanges = @(& $gitCommand.Source -C $repoRoot status --porcelain=v1 --untracked-files=normal)
        $sourceState = if ($LASTEXITCODE -eq 0 -and $sourceChanges.Count -eq 0) { 'git-clean' } else { 'git-modified' }
    }
}

$manifest = [ordered]@{
    product = 'DiskScope'
    version = $version
    runtime = $Runtime
    buildType = 'local-source'
    sourceCommit = $sourceCommit
    sourceState = $sourceState
    dotnetSdk = $dotnetSdkVersion
    testsRun = -not $SkipTests
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    executable = [ordered]@{
        name = 'DiskScope.exe'
        bytes = (Get-Item -LiteralPath $publishedExecutable).Length
        sha256 = $executableHash
    }
    portableArchive = [ordered]@{
        name = [System.IO.Path]::GetFileName($portableZip)
        bytes = (Get-Item -LiteralPath $portableZip).Length
        sha256 = $zipHash
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if ($Action -eq 'Install') {
    Assert-NotReparsePoint -Path $sourceInstallLocations.InstallRoot
    Assert-NotReparsePoint -Path $sourceInstallLocations.InstalledExecutable
    if ((Test-Path -LiteralPath $sourceInstallLocations.InstalledExecutable) -and
        -not (Test-Path -LiteralPath $sourceInstallLocations.InstalledExecutable -PathType Leaf)) {
        throw 'The expected source-build executable path is not a file. Nothing was overwritten.'
    }
    $installRootExists = Test-Path -LiteralPath $sourceInstallLocations.InstallRoot -PathType Container
    if ($installRootExists -and -not (Test-Path -LiteralPath $sourceInstallLocations.MarkerPath -PathType Leaf)) {
        $existingItems = @(Get-ChildItem -LiteralPath $sourceInstallLocations.InstallRoot -Force)
        if ($existingItems.Count -gt 0) {
            throw 'The source-build install directory contains files not owned by this builder. Nothing was overwritten.'
        }
    }

    New-Item -ItemType Directory -Path $sourceInstallLocations.InstallRoot -Force | Out-Null
    Copy-Item -LiteralPath $publishedExecutable -Destination $sourceInstallLocations.InstalledExecutable -Force
    $installMarker = [ordered]@{
        product = 'DiskScope'
        installType = 'source-build'
        version = $version
        sourceCommit = $sourceCommit
        sourceState = $sourceState
        sha256 = $executableHash
        installedUtc = [DateTime]::UtcNow.ToString('o')
    }
    $installMarker | ConvertTo-Json | Set-Content -LiteralPath $sourceInstallLocations.MarkerPath -Encoding utf8

    New-WindowsShortcut `
        -ShortcutPath $sourceInstallLocations.StartMenuShortcut `
        -TargetPath $sourceInstallLocations.InstalledExecutable `
        -WorkingDirectory $sourceInstallLocations.InstallRoot
    if (-not $NoDesktopShortcut -and -not [string]::IsNullOrWhiteSpace($sourceInstallLocations.DesktopShortcut)) {
        New-WindowsShortcut `
            -ShortcutPath $sourceInstallLocations.DesktopShortcut `
            -TargetPath $sourceInstallLocations.InstalledExecutable `
            -WorkingDirectory $sourceInstallLocations.InstallRoot
    }

    Write-Host "DiskScope source build installed at $($sourceInstallLocations.InstallRoot)" -ForegroundColor Green
    Write-Host 'Remove it later with: Build-From-Source.cmd -Action Uninstall'
} else {
    Write-Host "Portable source build created at $artifactRoot" -ForegroundColor Green
}

Write-Host "Executable SHA-256: $executableHash"
Write-Host "Archive SHA-256:    $zipHash"

if ($OpenOutput) {
    Start-Process -FilePath 'explorer.exe' -ArgumentList @("`"$artifactRoot`"")
}

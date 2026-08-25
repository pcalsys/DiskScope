[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [Parameter()]
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactBase = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\release'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactBase $Version))
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'portable'))
$projectPath = Join-Path $repoRoot 'src\DiskScope\DiskScope.csproj'

if (-not $artifactRoot.StartsWith($artifactBase + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved artifact path is outside the repository release directory.'
}

$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = [string]$project.Project.PropertyGroup.Version
if ($projectVersion -ne $Version) {
    throw "Requested version $Version does not match the project version $projectVersion."
}

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) { $dotnetCommand.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'The .NET SDK was not found.' }
& $dotnet restore (Join-Path $repoRoot 'DiskScope.sln') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& $dotnet restore $projectPath --runtime $Runtime
if ($LASTEXITCODE -ne 0) { throw 'Runtime-specific dotnet restore failed.' }

& $dotnet format (Join-Path $repoRoot 'DiskScope.sln') --verify-no-changes --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'dotnet format verification failed.' }

& $dotnet build (Join-Path $repoRoot 'DiskScope.sln') -c Release --no-restore --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

& $dotnet test (Join-Path $repoRoot 'DiskScope.sln') -c Release --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

& $dotnet publish $projectPath `
    -c Release -r $Runtime --self-contained true --no-restore `
    -o $publishRoot `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$publishedExecutable = Join-Path $publishRoot 'DiskScope.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable)) { throw 'Published DiskScope executable is missing.' }

$privateBuildRoots = @($repoRoot, $env:USERPROFILE) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd('\') } |
    Sort-Object -Unique
foreach ($publishedFile in Get-ChildItem -LiteralPath $publishRoot -Recurse -File) {
    $publishedBytes = [System.IO.File]::ReadAllBytes($publishedFile.FullName)
    $publishedAsciiText = [System.Text.Encoding]::ASCII.GetString($publishedBytes)
    $publishedUnicodeText = [System.Text.Encoding]::Unicode.GetString($publishedBytes)
    foreach ($privateBuildRoot in $privateBuildRoots) {
        $containsAsciiPath = $publishedAsciiText.IndexOf(
            $privateBuildRoot,
            [StringComparison]::OrdinalIgnoreCase) -ge 0
        $containsUnicodePath = $publishedUnicodeText.IndexOf(
            $privateBuildRoot,
            [StringComparison]::OrdinalIgnoreCase) -ge 0
        if ($containsAsciiPath -or $containsUnicodePath) {
            throw "Published payload contains a machine-local build path: $($publishedFile.Name)"
        }
    }
}

$smokeProcess = Start-Process -FilePath $publishedExecutable -ArgumentList '--ui-smoke-test' -PassThru -WindowStyle Hidden
try {
    if (-not $smokeProcess.WaitForExit(30000)) {
        $smokeProcess.Kill($true)
        $smokeProcess.WaitForExit()
        throw 'Published UI smoke test timed out after 30 seconds.'
    }
    if ($smokeProcess.ExitCode -ne 0) { throw "Published UI smoke test failed with exit code $($smokeProcess.ExitCode)." }
} finally {
    $smokeProcess.Dispose()
}

$publishedExecutableReady = $false
for ($attempt = 0; $attempt -lt 50; $attempt++) {
    try {
        $exclusiveHandle = [System.IO.File]::Open($publishedExecutable, 'Open', 'Read', 'None')
        $exclusiveHandle.Dispose()
        $publishedExecutableReady = $true
        break
    } catch [System.IO.IOException] {
        Start-Sleep -Milliseconds 200
    }
}
if (-not $publishedExecutableReady) { throw 'Published executable remained locked after the UI smoke test.' }

$portableZip = Join-Path $artifactRoot "DiskScope-$Version-$Runtime-portable.zip"
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $portableZip -CompressionLevel Optimal

$innoCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$innoCompiler = $innoCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $innoCompiler) {
    throw 'Inno Setup 6 was not found. Install it before building official release artifacts.'
}

& $innoCompiler "/DMyAppVersion=$Version" "/DSourceDir=$publishRoot" "/DOutputDir=$artifactRoot" (Join-Path $repoRoot 'installer\DiskScope.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$downloads = Get-ChildItem -LiteralPath $artifactRoot -File | Where-Object { $_.Extension -in '.exe', '.zip' } | Sort-Object Name
$checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
$checksumLines = foreach ($download in $downloads) {
    $hash = Get-FileHash -LiteralPath $download.FullName -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $download.Name
}
$checksumLines | Set-Content -LiteralPath $checksumPath -Encoding ascii

$manifest = [ordered]@{
    product = 'DiskScope'
    version = $Version
    runtime = $Runtime
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    files = @($downloads | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        [ordered]@{ name = $_.Name; bytes = $_.Length; sha256 = $hash.Hash.ToLowerInvariant() }
    })
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifactRoot 'release-manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repoRoot 'RELEASE_NOTES.md') -Destination (Join-Path $artifactRoot 'RELEASE_NOTES.md')

Write-Host "Release artifacts created at $artifactRoot"
Get-ChildItem -LiteralPath $artifactRoot -File | Select-Object Name, Length

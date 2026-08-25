[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$releaseRoot = [System.IO.Path]::GetFullPath($ReleaseDirectory)
$installerPath = Join-Path $releaseRoot "DiskScope-$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer is missing: $installerPath"
}

$testBase = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'DiskScope.InstallerSmoke'))
$installRoot = [System.IO.Path]::GetFullPath((Join-Path $testBase ([Guid]::NewGuid().ToString('N'))))
if (-not $installRoot.StartsWith($testBase + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved installer smoke-test path is outside the intended temporary directory.'
}

New-Item -ItemType Directory -Path $testBase -Force | Out-Null
$installedExecutable = Join-Path $installRoot 'DiskScope.exe'
$uninstaller = Join-Path $installRoot 'unins000.exe'
$uninstallCompleted = $false

try {
    $installArguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        "/DIR=`"$installRoot`""
    )
    $installProcess = Start-Process -FilePath $installerPath -ArgumentList $installArguments -PassThru -Wait -WindowStyle Hidden
    try {
        if ($installProcess.ExitCode -ne 0) {
            throw "Installer returned exit code $($installProcess.ExitCode)."
        }
    } finally {
        $installProcess.Dispose()
    }

    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
        throw 'The installer completed without creating DiskScope.exe.'
    }
    if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        throw 'The installer completed without creating its uninstaller.'
    }

    $smokeProcess = Start-Process -FilePath $installedExecutable -ArgumentList '--ui-smoke-test' -PassThru -WindowStyle Hidden
    try {
        if (-not $smokeProcess.WaitForExit(30000)) {
            $smokeProcess.Kill($true)
            $smokeProcess.WaitForExit()
            throw 'Installed UI smoke test timed out after 30 seconds.'
        }
        if ($smokeProcess.ExitCode -ne 0) {
            throw "Installed UI smoke test returned exit code $($smokeProcess.ExitCode)."
        }
    } finally {
        $smokeProcess.Dispose()
    }

    $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait -WindowStyle Hidden
    try {
        if ($uninstallProcess.ExitCode -ne 0) {
            throw "Uninstaller returned exit code $($uninstallProcess.ExitCode)."
        }
    } finally {
        $uninstallProcess.Dispose()
    }
    $uninstallCompleted = $true

    for ($attempt = 0; $attempt -lt 50 -and (Test-Path -LiteralPath $installedExecutable); $attempt++) {
        Start-Sleep -Milliseconds 200
    }
    if (Test-Path -LiteralPath $installedExecutable) {
        throw 'DiskScope.exe remains after uninstall completed.'
    }
} finally {
    if (-not $uninstallCompleted -and (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        try {
            $cleanupProcess = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait -WindowStyle Hidden
            $cleanupProcess.Dispose()
        } catch {
            Write-Warning "Best-effort uninstaller cleanup failed: $($_.Exception.Message)"
        }
    }

    $validatedInstallRoot = [System.IO.Path]::GetFullPath($installRoot)
    if (-not $validatedInstallRoot.StartsWith($testBase + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clean an installer smoke-test path outside the intended temporary directory.'
    }
    if (Test-Path -LiteralPath $validatedInstallRoot) {
        Remove-Item -LiteralPath $validatedInstallRoot -Recurse -Force
    }
    if ((Test-Path -LiteralPath $testBase) -and -not (Get-ChildItem -LiteralPath $testBase -Force)) {
        Remove-Item -LiteralPath $testBase -Force
    }
}

Write-Host 'Installer install, installed-app UI smoke test, and uninstall verification passed.'

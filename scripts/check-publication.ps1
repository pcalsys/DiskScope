[CmdletBinding()]
param(
    [Parameter()]
    [switch]$CheckCommitEmails,

    [Parameter()]
    [string[]]$AllowedSyntheticUserProfiles = @('Alice', 'Sample')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Category, [string]$Path) {
    $script:failures.Add("${Category}: $Path")
}

Push-Location $repoRoot
try {
    $paths = @(& git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate repository files.' }

    foreach ($relativePath in $paths) {
        $normalizedPath = $relativePath.Replace('\', '/')
        if ($normalizedPath -match '(^|/)(bin|obj|artifacts|TestResults|\.vs)(/|$)') {
            Add-Failure 'Generated output is publishable' $normalizedPath
            continue
        }

        $isEnvironmentFile = $normalizedPath -match '(?i)(^|/)\.env($|\.)' `
            -and $normalizedPath -notmatch '(?i)\.env\.example$'
        if ($normalizedPath -match '(?i)\.(pfx|p12|snk|pem|key|suo|user)$' -or $isEnvironmentFile) {
            Add-Failure 'Private or machine-local file type is publishable' $normalizedPath
            continue
        }

        $fullPath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { continue }
        $file = Get-Item -LiteralPath $fullPath
        if ($file.Length -gt 50MB) {
            Add-Failure 'File exceeds the 50 MiB repository limit' $normalizedPath
            continue
        }

        $bytes = [System.IO.File]::ReadAllBytes($fullPath)
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)

        foreach ($match in [regex]::Matches($text, '(?i)[A-Z]:\\Users\\(?<profile>[^\\/"''\s]+)')) {
            if ($AllowedSyntheticUserProfiles -notcontains $match.Groups['profile'].Value) {
                Add-Failure 'Local Windows user path' $normalizedPath
            }
        }

        if ($text -match '(?i)(?:/Users/|/home/)[A-Za-z0-9._-]+') {
            Add-Failure 'Local Unix user path' $normalizedPath
        }

        $currentUsernamePattern = if (-not [string]::IsNullOrWhiteSpace($env:USERNAME)) {
            '(?i)(?<![A-Z0-9_])' + [regex]::Escape($env:USERNAME) + '(?![A-Z0-9_])'
        } else {
            '(?!)'
        }
        $containsCurrentUsername = $env:USERNAME.Length -ge 3 `
            -and $env:USERNAME -notin $AllowedSyntheticUserProfiles `
            -and [regex]::IsMatch($text, $currentUsernamePattern)
        if ($containsCurrentUsername) {
            Add-Failure 'Current local username' $normalizedPath
        }

        foreach ($email in [regex]::Matches($text, '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b')) {
            if (-not $email.Value.EndsWith('@users.noreply.github.com', [StringComparison]::OrdinalIgnoreCase)) {
                Add-Failure 'Email address' $normalizedPath
            }
        }

        $githubTokenPattern = '(?i)\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github' + '_pat_[A-Za-z0-9_]{20,})\b'
        $privateKeyPattern = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE' + ' KEY-----'
        $secretAssignmentPattern = '(?i)\b(?:password|passwd|api[_-]?key|client[_-]?secret|access[_-]?token|auth[_-]?token)\b\s*[:=]\s*["''][^"'']{6,}["'']'
        if ([regex]::IsMatch($text, $githubTokenPattern)) { Add-Failure 'GitHub token' $normalizedPath }
        if ([regex]::IsMatch($text, $privateKeyPattern)) { Add-Failure 'Private key' $normalizedPath }
        if ([regex]::IsMatch($text, '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b')) { Add-Failure 'AWS access key' $normalizedPath }
        if ([regex]::IsMatch($text, $secretAssignmentPattern)) { Add-Failure 'Hard-coded secret assignment' $normalizedPath }
        if ([regex]::IsMatch($text, '(?i)https?://[^/\s:@]+:[^/\s@]+@')) { Add-Failure 'Credential-bearing URL' $normalizedPath }
    }

    if ($CheckCommitEmails) {
        $commitEmails = @(& git log --format='%ae%n%ce')
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect commit metadata.' }
        foreach ($email in $commitEmails | Sort-Object -Unique) {
            if ($email -notmatch '(?i)(?:@users\.noreply\.github\.com>?$|^noreply@github\.com$)') {
                Add-Failure 'Commit metadata contains a non-noreply email' '(Git history)'
            }
        }

        $taggerEmails = @(& git for-each-ref --format='%(taggeremail)' refs/tags)
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect tag metadata.' }
        foreach ($email in $taggerEmails | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique) {
            if ($email -notmatch '(?i)(?:@users\.noreply\.github\.com>?$|^noreply@github\.com$)') {
                Add-Failure 'Tag metadata contains a non-noreply email' '(Git history)'
            }
        }
    }
} finally {
    Pop-Location
}

$uniqueFailures = @($failures | Sort-Object -Unique)
if ($uniqueFailures.Count -gt 0) {
    $uniqueFailures | ForEach-Object { Write-Host "ERROR  $_" -ForegroundColor Red }
    throw "$($uniqueFailures.Count) publication hygiene check(s) failed. No matched values were printed."
}

Write-Host "Publication hygiene passed for $($paths.Count) files; no private-data or repository-output markers were detected."

# Build and install DiskScope from source

DiskScope can be compiled locally from the public repository instead of using a prebuilt download. The result is still a Windows executable, but that executable is created on your own computer from code you can inspect first.

## What you need

- Windows 10 or Windows 11 on an x64 computer
- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- The complete DiskScope source code, either from `git clone` or GitHub's **Code > Download ZIP** option
- An internet connection for the initial NuGet restore; DiskScope itself does not require a network connection

The builder does not download or execute another installer, does not request administrator rights, and does not require Inno Setup. Dependency versions used by the tests are recorded in the repository lock file.

## Easiest option: portable source build

1. Inspect `Build-From-Source.cmd` and `scripts/build-from-source.ps1` if desired.
2. Double-click `Build-From-Source.cmd`.
3. Wait for restore, compilation, tests, packaging, and the local-path privacy check to finish.
4. The output folder opens automatically. Run `DiskScope.exe` from the versioned folder or extract the portable ZIP.

Output is written below `artifacts/source-build/<version>/`. The builder also writes `SHA256SUMS.txt` and `source-build-manifest.json` so the exact result, SDK version, source commit, clean/modified source state, and hashes can be inspected.

## Optional per-user installation

Open Command Prompt or PowerShell in the repository and run:

```powershell
.\Build-From-Source.cmd -Action Install
```

This builds from source first, copies the locally produced `DiskScope.exe` and a small ownership manifest to `%LOCALAPPDATA%\Programs\DiskScope Source Build`, and creates clearly named Start menu and desktop shortcuts. It does not use administrator rights, the registry, Windows services, or the prebuilt setup package. The source-build installation is separate from the official installer location.

To omit the desktop shortcut:

```powershell
.\Build-From-Source.cmd -Action Install -NoDesktopShortcut
```

To remove only the source-build installation and its shortcuts:

```powershell
.\Build-From-Source.cmd -Action Uninstall
```

The uninstall action requires the builder's ownership marker. It refuses to delete an unrecognized directory.

## Manual build commands

The wrapper is optional. These are the essential commands it automates:

```powershell
dotnet restore DiskScope.sln --locked-mode
dotnet build DiskScope.sln -c Release --no-restore
dotnet test DiskScope.sln -c Release --no-build
dotnet restore src/DiskScope/DiskScope.csproj --runtime win-x64
dotnet publish src/DiskScope/DiskScope.csproj -c Release -r win-x64 --self-contained true --no-restore -o artifacts/source-build/manual/DiskScope -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```

## Code overview

| Area | Purpose |
|---|---|
| `src/DiskScope.Core/` | Drive discovery, iterative scanning, size aggregation, file types, and conservative safety classification |
| `src/DiskScope/` | WPF interface, Windows theme synchronization, German/English localization, settings, and Windows file actions |
| `tests/DiskScope.Tests/` | Automated tests for scanner, aggregation, classification, cancellation, and edge cases |
| `tools/` | Reproducible logo generation and automated WPF rendering checks |
| `scripts/build-from-source.ps1` | Transparent local build, optional per-user installation, checksums, and safe uninstall action |
| `installer/` | Definition for the separate prebuilt Inno Setup package; it is not used by the source builder |
| `.github/workflows/` | Public Windows CI and release automation |

Read [ARCHITECTURE.md](ARCHITECTURE.md) for the scan flow and trust boundaries, [SAFETY_MODEL.md](SAFETY_MODEL.md) for deletion protections, and the repository's [privacy statement](../PRIVACY.md) before running or changing the application.

## Security notes

- Review the source and the builder at the tag or commit you intend to use.
- The builder fails if the output contains the repository path or current Windows user-profile path.
- Locally built files are not Authenticode-signed, so Windows SmartScreen or security software may still warn.
- Never disable security software merely to complete a build. Investigate a warning and compare the source commit and generated SHA-256 values.

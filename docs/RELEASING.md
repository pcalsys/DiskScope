# Release process

## Preconditions

- Worktree is clean and the intended commit is checked out.
- Version in `src/DiskScope/DiskScope.csproj`, `CHANGELOG.md`, and release notes agrees.
- .NET 8 SDK and Inno Setup 6 are installed on Windows.
- Public binaries are Authenticode-signed when a protected signing certificate is available.

## Local verification

```powershell
dotnet restore DiskScope.sln --locked-mode
dotnet build DiskScope.sln -c Release --no-restore
dotnet test DiskScope.sln -c Release --no-build
./scripts/check-publication.ps1 -CheckCommitEmails
./scripts/build-release.ps1 -Version 1.0.0
./scripts/test-installer.ps1 -ReleaseDirectory artifacts/release/1.0.0 -Version 1.0.0
```

On a machine whose Windows PowerShell execution policy blocks local scripts, invoke the reviewed script with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build-release.ps1 -Version 1.0.0`.

Extract the portable archive into a clean folder. Launch it, scan a synthetic tree, verify sorting/filtering/cancellation, automatic Windows light/dark switching, both manual themes, German/English switching, the new logo in the window and shortcut, and non-destructive shell actions. Then verify the installer install/launch/uninstall cycle.

Compare every downloadable file with `SHA256SUMS.txt`; `verify-release.ps1` also checks that the manifest agrees with the files and hashes.

`check-publication.ps1` rejects credentials, personal user paths, private key material, generated output, and non-noreply release commit metadata. The release builder also fails if the published payload contains the repository path or the build account's user-profile path.

`test-installer.ps1` installs the setup into an isolated temporary per-user directory, runs the installed application's UI smoke test, invokes the generated uninstaller, and verifies that the installed executable is removed.

## GitHub release

1. Commit the final verified tree.
2. Create annotated tag `v1.0.0` on that commit.
3. Push the branch and tag.
4. The `release.yml` workflow rebuilds/tests/packages and creates the GitHub release from `RELEASE_NOTES.md`.
5. Confirm the installer, portable ZIP, checksums, and manifest are attached and downloadable.

Never claim a release succeeded until GitHub shows the published release and all assets.

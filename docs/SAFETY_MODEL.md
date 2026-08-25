# Safety classification model

DiskScope's safety assessment is deliberately conservative. It answers “what does this location and file type suggest?”—not “is this safe to delete?”

## Category order

Rules are evaluated from highest protection to lowest:

1. **Critical system file** — Windows boot, registry hive, component-store, protected volume metadata, system-drive boot/recovery locations, root paging/hibernation files, and selected core process locations/names. Recycle Bin action is blocked.
2. **Windows system file** — anything else under the active Windows directory or a recognized Windows upgrade/recovery-work directory. Recycle Bin action is blocked.
3. **Program file** — items under Program Files directories. Users are directed to Windows uninstallation.
4. **Temporary file** — active temp paths or explicit temporary directory segments. Removal is not described as guaranteed safe.
5. **Application data** — Local/Roaming AppData or ProgramData. An elevated warning is required.
6. **Personal file** — common user-content folders such as Desktop, Documents, Downloads, Pictures, Music, Videos, and OneDrive.
7. **Program-related file** — executable/script/component extensions in otherwise unclassified locations. An elevated warning is required.
8. **Unknown file** — every remaining item. An elevated warning is required.

Location checks are normalized, case-insensitive, and directory-boundary aware. A prefix such as `C:\Windows.old` does not match `C:\Windows`.

## Deletion policy

DiskScope never permanently deletes files. Eligible actions go through the Windows Recycle Bin. Critical and Windows categories are blocked in both UI state and the event handler. Program, application-data, executable, and unknown categories always show a confirmation warning even if general confirmations are disabled.

Recycle actions are restricted to local fixed drives. DiskScope refuses the action for network, optical, RAM, and removable-drive locations where Windows cannot guarantee a recoverable local Recycle Bin.

## Limitations

- A personal file can still be important.
- Temporary-looking data can still be in use or needed for recovery.
- Portable applications may live outside Program Files.
- Malware can use trusted-looking locations, names, or extensions.
- Filesystem permissions and files can change after classification. Reparse points are excluded from scan results.

DiskScope is not antivirus software, a backup, a digital-signature validator, or a substitute for vendor instructions.

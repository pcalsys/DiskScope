# DiskScope privacy statement

Effective: 25 August 2026

DiskScope is designed to operate entirely on the Windows computer where it is launched.

## Data processed

To produce scan results, DiskScope reads filesystem metadata such as file and folder names, paths, sizes, attributes, extensions, and modification times. It does not read file contents for classification.

The selected theme, scan visibility options, recycle-bin confirmation preference, and size-format preference are saved in `%LOCALAPPDATA%\DiskScope\settings.json`.

## Data transmission

DiskScope does not transmit data. The application contains no account system, cloud backend, telemetry, analytics, advertising, crash-upload, or update service. File names, file paths, scan results, and settings do not leave the computer through DiskScope.

Normal use does not require an internet connection. Opening the GitHub project from the About page launches the user's default browser and is the only explicit link to an online location.

## File actions

When requested by the user, DiskScope asks Windows to open a file, reveal it in Explorer, display its properties, copy its path to the local clipboard, or move it to the local Recycle Bin. DiskScope never permanently deletes a file.

## Retention and removal

Scan results are held in application memory and are discarded when a new scan begins or the application closes. DiskScope does not persist scan history.

Uninstalling DiskScope removes application binaries. To remove local preferences as well, delete `%LOCALAPPDATA%\DiskScope\settings.json`.

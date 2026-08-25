# Changelog

All notable changes to DiskScope are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-08-25

### Added

- Automatic Windows app-theme detection that updates DiskScope while it is running, with optional light/dark overrides.
- Complete live German/English localization with an in-app language selector and Windows-language default.
- A unified hard-drive-and-magnifier logo for the application UI, executable, installer, and shortcuts.
- Local drive and folder scanning with streaming progress and cancellation.
- Size-descending file results with sortable columns, search, size thresholds, and category filters.
- Nested folder-size aggregation and largest-folder view.
- Conservative personal, temporary, application-data, program, unknown, Windows, and critical-system assessments.
- Dedicated safety explanation and recommendation panel.
- Open, Explorer reveal, copy path, Windows properties, and Recycle Bin actions.
- Deletion hard blocks for detected Windows, protected volume metadata, boot/recovery locations, and critical system files.
- Drive overview, light/dark/system themes, local settings, and privacy/about pages.
- Iterative scan traversal with inaccessible-item isolation and complete reparse-point exclusion.
- Self-contained portable and per-user installer packaging with SHA-256 checksums.
- Automated build, test, and tag-based release workflows.
- Publication hygiene gates for secrets, machine-local paths, private commit metadata, and generated output.
- Automated install, installed-app UI smoke test, and uninstall verification for release installers.

[1.0.0]: https://github.com/pcalsys/DiskScope/releases/tag/v1.0.0

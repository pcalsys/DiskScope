# DiskScope architecture

DiskScope separates filesystem analysis from Windows presentation and file actions.

## Components

- `DiskScope.Core` targets plain .NET 8. It owns immutable models, drive discovery, file-type mapping, safety classification, iterative traversal, progress, cancellation, and folder aggregation.
- `DiskScope` targets .NET 8 WPF. It owns the virtualized interface, live German/English resources, local settings, Windows-theme synchronization, shell actions, user confirmations, and safety-panel presentation.
- `DiskScope.Tests` exercises the core without starting a desktop session.
- `DiskScope.ScreenshotGenerator` is a development-only WPF host that renders every page in both languages and themes without adding rendering commands to the production executable.
- `DiskScope.AssetGenerator` reproducibly derives the multi-size Windows icon and 512-pixel UI/README image from the transparent source artwork.

## Scan flow

1. The UI captures immutable scan options and starts the core scanner on a worker thread.
2. The scanner uses an explicit directory stack, avoiding call-stack exhaustion on deeply nested trees.
3. Every directory and file metadata read has an expected filesystem exception boundary. A denied or disappearing item increments the skipped count without ending the scan.
4. File and directory reparse points are always skipped, preventing junction cycles, link-based safety-classification bypasses, and accidental traversal outside the selected tree.
5. Files are emitted in batches to bound dispatcher traffic. Live rows are unsorted while scanning; one size-descending sort is applied on completion to avoid repeated whole-result sorts.
6. Folder totals are accumulated for each ancestor up to the selected root. Only the largest 100 are retained for presentation after scanning.
7. Cancellation returns a valid partial result instead of discarding work.

## Scale characteristics

Traversal is sequential and metadata-only. The scanner does not open file contents and does not enqueue the complete directory tree at once. Result rows remain in memory so every discovered file is searchable and sortable; WPF row and column virtualization prevents visual containers from being created for off-screen rows.

Safety assessments are shared immutable objects per category, avoiding repeated explanation strings across large scans. Search input is debounced, and default sorting is deferred until scan completion.

## Trust boundaries

DiskScope runs with the current user's permissions and does not request elevation. It treats the filesystem as mutable: files may disappear and removable drives may disconnect. Safety classification is deterministic local guidance and never overrides Windows ACLs.

No component has a network dependency. The About page can explicitly ask the default browser to open the source repository.

Continuous integration renders every page through the separate screenshot host in German and English with light and dark resources, so missing runtime XAML resources or binding failures stop the build. The release pipeline additionally runs a non-interactive `--ui-smoke-test` against the self-contained published executable before packaging.

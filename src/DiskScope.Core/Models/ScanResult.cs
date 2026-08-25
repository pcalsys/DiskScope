namespace DiskScope.Core.Models;

public sealed record ScanResult(
    string RootPath,
    long FileCount,
    long TotalBytes,
    long FolderCount,
    long SkippedItems,
    TimeSpan Duration,
    IReadOnlyList<FolderSummary> LargestFolders,
    bool WasCancelled);

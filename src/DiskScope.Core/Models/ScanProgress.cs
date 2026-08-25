namespace DiskScope.Core.Models;

public sealed record ScanProgress(
    long FilesFound,
    long BytesFound,
    long FoldersVisited,
    long SkippedItems,
    string CurrentPath,
    TimeSpan Elapsed);

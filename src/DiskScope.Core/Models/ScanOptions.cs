namespace DiskScope.Core.Models;

public sealed record ScanOptions(
    bool IncludeHiddenFiles = false,
    bool IncludeSystemFiles = false,
    int BatchSize = 250);

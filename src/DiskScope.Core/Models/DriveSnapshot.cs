namespace DiskScope.Core.Models;

public sealed record DriveSnapshot(
    string Name,
    string DisplayName,
    DriveType DriveType,
    string Format,
    long TotalSize,
    long FreeSpace,
    bool IsReady)
{
    public long UsedSpace => Math.Max(0, TotalSize - FreeSpace);
    public double UsedFraction => TotalSize <= 0 ? 0 : (double)UsedSpace / TotalSize;
    public string UsageText => IsReady
        ? $"{ByteSizeFormatter.Format(UsedSpace)} of {ByteSizeFormatter.Format(TotalSize)} used"
        : "Drive is not ready";
}

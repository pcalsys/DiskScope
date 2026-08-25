namespace DiskScope.Core.Models;

public sealed record FolderSummary(string Name, string FullPath, long Size, long FileCount)
{
    public string SizeText => ByteSizeFormatter.Format(Size);
}

using System.Globalization;

namespace DiskScope.Core.Models;

public sealed record FileItem(
    string Name,
    string FullPath,
    string DirectoryPath,
    string Extension,
    string TypeName,
    FileCategory FileCategory,
    long Size,
    DateTime LastModified,
    FileAttributes Attributes,
    SafetyAssessment Safety)
{
    public string SizeText => ByteSizeFormatter.Format(Size);
    public string ModifiedText => LastModified.ToString("g", CultureInfo.CurrentCulture);
    public SafetyCategory SafetyCategory => Safety.Category;
}

namespace DiskScope.Core.Models;

public sealed record ScanIssue(string Path, string Message, string ErrorType);

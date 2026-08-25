using DiskScope.Core.Models;

namespace DiskScope.Core.Services;

public static class FileTypeClassifier
{
    private static readonly HashSet<string> Videos = Set(".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpeg", ".mpg");
    private static readonly HashSet<string> Images = Set(".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".heic", ".svg", ".raw");
    private static readonly HashSet<string> Archives = Set(".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab");
    private static readonly HashSet<string> Programs = Set(".exe", ".dll", ".msi", ".msix", ".appx", ".sys", ".com", ".bat", ".cmd", ".ps1", ".jar");
    private static readonly HashSet<string> Documents = Set(".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".md", ".csv", ".odt", ".ods", ".epub");
    private static readonly HashSet<string> Audio = Set(".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".opus");

    public static FileCategory Classify(string extension)
    {
        var normalized = Normalize(extension);
        if (Videos.Contains(normalized)) return FileCategory.Video;
        if (Images.Contains(normalized)) return FileCategory.Image;
        if (Archives.Contains(normalized)) return FileCategory.Archive;
        if (Programs.Contains(normalized)) return FileCategory.Program;
        if (Documents.Contains(normalized)) return FileCategory.Document;
        if (Audio.Contains(normalized)) return FileCategory.Audio;
        return FileCategory.Other;
    }

    public static string GetTypeName(string extension)
    {
        var normalized = Normalize(extension);
        if (string.IsNullOrEmpty(normalized)) return "File";
        return $"{normalized.TrimStart('.').ToUpperInvariant()} file";
    }

    private static string Normalize(string extension) => string.IsNullOrWhiteSpace(extension)
        ? string.Empty
        : extension[0] == '.' ? extension : $".{extension}";

    private static HashSet<string> Set(params string[] extensions) => new(extensions, StringComparer.OrdinalIgnoreCase);
}

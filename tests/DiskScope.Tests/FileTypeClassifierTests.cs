using DiskScope.Core.Models;
using DiskScope.Core.Services;

namespace DiskScope.Tests;

public sealed class FileTypeClassifierTests
{
    [Theory]
    [InlineData(".mkv", FileCategory.Video)]
    [InlineData("JPG", FileCategory.Image)]
    [InlineData(".7z", FileCategory.Archive)]
    [InlineData(".exe", FileCategory.Program)]
    [InlineData(".docx", FileCategory.Document)]
    [InlineData(".flac", FileCategory.Audio)]
    [InlineData(".something", FileCategory.Other)]
    public void Classify_RecognizesCommonExtensions(string extension, FileCategory expected)
    {
        Assert.Equal(expected, FileTypeClassifier.Classify(extension));
    }
}

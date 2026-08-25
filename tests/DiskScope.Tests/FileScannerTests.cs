using DiskScope.Core.Models;
using DiskScope.Core.Services;

namespace DiskScope.Tests;

public sealed class FileScannerTests
{
    [Fact]
    public async Task ScanAsync_ReportsFilesAndAggregatesNestedFolders()
    {
        using var fixture = new TemporaryDirectory();
        var parent = Directory.CreateDirectory(Path.Combine(fixture.Path, "Parent"));
        var nested = Directory.CreateDirectory(Path.Combine(parent.FullName, "Nested"));
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "root.bin"), new byte[2], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(parent.FullName, "parent.bin"), new byte[3], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "nested.bin"), new byte[5], TestContext.Current.CancellationToken);

        var files = new List<FileItem>();
        var result = await new FileScanner().ScanAsync(
            fixture.Path,
            new ScanOptions(IncludeHiddenFiles: true, IncludeSystemFiles: true, BatchSize: 2),
            batch =>
            {
                files.AddRange(batch);
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.WasCancelled);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(10, result.TotalBytes);
        Assert.Equal(3, files.Count);
        Assert.Equal(8, result.LargestFolders.Single(folder => folder.Name == "Parent").Size);
        Assert.Equal(5, result.LargestFolders.Single(folder => folder.Name == "Nested").Size);
    }

    [Fact]
    public async Task ScanAsync_ReturnsPartialResultWhenCancelled()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "file.txt"), "data", TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        var result = await new FileScanner().ScanAsync(
            fixture.Path,
            new ScanOptions(),
            _ => Task.CompletedTask,
            cancellationToken: cancellation.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(0, result.FileCount);
    }

    [Fact]
    public async Task ScanAsync_RejectsNullOptions()
    {
        using var fixture = new TemporaryDirectory();

        await Assert.ThrowsAsync<ArgumentNullException>(() => new FileScanner().ScanAsync(
            fixture.Path,
            null!,
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanAsync_RejectsReparsePointRootWhenLinksAreAvailable()
    {
        using var fixture = new TemporaryDirectory();
        using var target = new TemporaryDirectory();
        var link = Path.Combine(fixture.Path, "linked-root");
        try
        {
            Directory.CreateSymbolicLink(link, target.Path);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        await Assert.ThrowsAsync<NotSupportedException>(() => new FileScanner().ScanAsync(
            link,
            new ScanOptions(IncludeHiddenFiles: true, IncludeSystemFiles: true),
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanAsync_HandlesDeepPathsAndUnicodeNames()
    {
        using var fixture = new TemporaryDirectory();
        var directory = fixture.Path;
        for (var index = 0; index < 10; index++)
        {
            directory = Path.Combine(directory, $"folder-{index:D2}-abcdefghijkl");
            Directory.CreateDirectory(directory);
        }
        var unusualPath = Path.Combine(directory, "résumé – 数据 – [final].txt");
        await File.WriteAllTextAsync(unusualPath, "DiskScope", TestContext.Current.CancellationToken);

        var files = new List<FileItem>();
        var result = await new FileScanner().ScanAsync(
            fixture.Path,
            new ScanOptions(IncludeHiddenFiles: true, IncludeSystemFiles: true),
            batch =>
            {
                files.AddRange(batch);
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.FileCount);
        Assert.Equal("résumé – 数据 – [final].txt", Assert.Single(files).Name);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScanAsync_StreamsLargeSyntheticTreeInBoundedBatches()
    {
        using var fixture = new TemporaryDirectory();
        const int fileCount = 2_500;
        for (var directoryIndex = 0; directoryIndex < 25; directoryIndex++)
        {
            var directory = Directory.CreateDirectory(Path.Combine(fixture.Path, $"Folder-{directoryIndex:D2}"));
            for (var fileIndex = 0; fileIndex < 100; fileIndex++)
            {
                using var stream = File.Create(Path.Combine(directory.FullName, $"file-{fileIndex:D3}.bin"));
                stream.SetLength(fileIndex + 1);
            }
        }

        var batchSizes = new List<int>();
        var result = await new FileScanner().ScanAsync(
            fixture.Path,
            new ScanOptions(IncludeHiddenFiles: true, IncludeSystemFiles: true, BatchSize: 128),
            batch =>
            {
                batchSizes.Add(batch.Count);
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(fileCount, result.FileCount);
        Assert.Equal(fileCount, batchSizes.Sum());
        Assert.True(batchSizes.Count > 1);
        Assert.All(batchSizes, size => Assert.InRange(size, 1, 128));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiskScope.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}

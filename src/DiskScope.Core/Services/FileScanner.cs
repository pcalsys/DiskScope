using System.Diagnostics;
using DiskScope.Core.Models;

namespace DiskScope.Core.Services;

public sealed class FileScanner
{
    private readonly SafetyClassifier _safetyClassifier;

    public FileScanner(SafetyClassifier? safetyClassifier = null)
    {
        _safetyClassifier = safetyClassifier ?? new SafetyClassifier();
    }

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        ScanOptions options,
        Func<IReadOnlyList<FileItem>, Task> onBatch,
        IProgress<ScanProgress>? progress = null,
        Action<ScanIssue>? onIssue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onBatch);

        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"The scan location does not exist: {root}");
        EnsurePathContainsNoReparsePoints(root);

        var stopwatch = Stopwatch.StartNew();
        var pendingFolders = new Stack<string>();
        var folderTotals = new Dictionary<string, FolderAccumulator>(StringComparer.OrdinalIgnoreCase);
        var batch = new List<FileItem>(Math.Max(1, options.BatchSize));
        long fileCount = 0;
        long totalBytes = 0;
        long foldersVisited = 0;
        long skippedItems = 0;
        var cancelled = false;
        var lastProgress = TimeSpan.Zero;

        pendingFolders.Push(root);
        folderTotals[root] = new FolderAccumulator();

        try
        {
            while (pendingFolders.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentFolder = pendingFolders.Pop();
                foldersVisited++;

                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(currentFolder);
                }
                catch (Exception exception) when (IsExpectedFileSystemException(exception))
                {
                    skippedItems++;
                    onIssue?.Invoke(new(currentFolder, exception.Message, exception.GetType().Name));
                    continue;
                }

                try
                {
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        FileAttributes attributes;
                        try
                        {
                            attributes = File.GetAttributes(entry);
                        }
                        catch (Exception exception) when (IsExpectedFileSystemException(exception))
                        {
                            skippedItems++;
                            onIssue?.Invoke(new(entry, exception.Message, exception.GetType().Name));
                            continue;
                        }

                        var isHidden = (attributes & FileAttributes.Hidden) != 0;
                        var isSystem = (attributes & FileAttributes.System) != 0;
                        if ((!options.IncludeHiddenFiles && isHidden) || (!options.IncludeSystemFiles && isSystem))
                        {
                            skippedItems++;
                            continue;
                        }

                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                            if (isReparsePoint)
                            {
                                skippedItems++;
                                continue;
                            }

                            pendingFolders.Push(entry);
                            folderTotals.TryAdd(entry, new FolderAccumulator());
                            continue;
                        }

                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            skippedItems++;
                            continue;
                        }

                        try
                        {
                            var info = new FileInfo(entry);
                            var extension = info.Extension;
                            var category = FileTypeClassifier.Classify(extension);
                            var item = new FileItem(
                                info.Name,
                                info.FullName,
                                info.DirectoryName ?? currentFolder,
                                extension,
                                FileTypeClassifier.GetTypeName(extension),
                                category,
                                info.Length,
                                info.LastWriteTime,
                                attributes,
                                _safetyClassifier.Assess(info.FullName, category));

                            batch.Add(item);
                            fileCount++;
                            totalBytes += item.Size;
                            AddToFolderTotals(folderTotals, item.DirectoryPath, root, item.Size);

                            if (batch.Count >= Math.Max(1, options.BatchSize))
                            {
                                await onBatch(batch.ToArray()).ConfigureAwait(false);
                                batch.Clear();
                            }
                        }
                        catch (Exception exception) when (IsExpectedFileSystemException(exception))
                        {
                            skippedItems++;
                            onIssue?.Invoke(new(entry, exception.Message, exception.GetType().Name));
                        }

                        if (stopwatch.Elapsed - lastProgress >= TimeSpan.FromMilliseconds(150))
                        {
                            progress?.Report(new(fileCount, totalBytes, foldersVisited, skippedItems, currentFolder, stopwatch.Elapsed));
                            lastProgress = stopwatch.Elapsed;
                        }
                    }
                }
                catch (Exception exception) when (IsExpectedFileSystemException(exception))
                {
                    skippedItems++;
                    onIssue?.Invoke(new(currentFolder, exception.Message, exception.GetType().Name));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }

        if (batch.Count > 0)
        {
            await onBatch(batch.ToArray()).ConfigureAwait(false);
        }

        stopwatch.Stop();
        progress?.Report(new(fileCount, totalBytes, foldersVisited, skippedItems, root, stopwatch.Elapsed));

        var largestFolders = folderTotals
            .Where(pair => !pair.Key.Equals(root, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Value.Size)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(pair => new FolderSummary(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(pair.Key)) is { Length: > 0 } name ? name : pair.Key,
                pair.Key,
                pair.Value.Size,
                pair.Value.FileCount))
            .ToArray();

        return new(root, fileCount, totalBytes, foldersVisited, skippedItems, stopwatch.Elapsed, largestFolders, cancelled);
    }

    private static void AddToFolderTotals(Dictionary<string, FolderAccumulator> totals, string directory, string root, long size)
    {
        var current = directory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (!totals.TryGetValue(current, out var accumulator))
            {
                accumulator = new FolderAccumulator();
                totals[current] = accumulator;
            }

            accumulator.Size += size;
            accumulator.FileCount++;
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || !IsWithin(parent, root)) break;
            current = parent;
        }
    }

    private static bool IsWithin(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePathContainsNoReparsePoints(string path)
    {
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new NotSupportedException(
                    "DiskScope does not scan locations reached through a filesystem link or mount point. Choose the physical folder instead.");
            }
        }
    }

    private static bool IsExpectedFileSystemException(Exception exception) => exception is
        UnauthorizedAccessException or IOException or PathTooLongException or DirectoryNotFoundException or FileNotFoundException or NotSupportedException;

    private sealed class FolderAccumulator
    {
        public long Size { get; set; }
        public long FileCount { get; set; }
    }
}

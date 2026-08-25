using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using DiskScope.Core;
using DiskScope.Core.Models;
using DiskScope.Core.Services;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.ViewModels;

public enum AppPage
{
    Overview,
    Results,
    Settings,
    About
}

public sealed record SizeFilterOption(string Label, long MinimumBytes);
public sealed record CategoryFilterOption(string Label, FileCategory? Category);
public sealed record ThemeModeOption(string Label, ThemeMode Mode);
public sealed record SizeDisplayModeOption(string Label, FileSizeDisplayMode Mode);
public sealed record LanguageModeOption(string Label, AppLanguage Language);

public sealed class MainViewModel : ObservableObject
{
    private enum ScanStatus
    {
        Ready,
        Scanning,
        Cancelled,
        Complete,
        Failed
    }

    private readonly FileScanner _scanner = new();
    private readonly SettingsService _settingsService;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _searchDebounce;
    private AppLanguage _appliedLanguage;
    private CancellationTokenSource? _scanCancellation;
    private AppPage _currentPage;
    private bool _isScanning;
    private string _searchText = string.Empty;
    private SizeFilterOption _selectedSizeFilter;
    private CategoryFilterOption _selectedCategoryFilter;
    private FileItem? _selectedFile;
    private FolderSummary? _selectedFolder;
    private ScanStatus _scanStatus;
    private string _progressPath = string.Empty;
    private string _currentPath = string.Empty;
    private long _fileCount;
    private long _totalBytes;
    private long _folderCount;
    private long _skippedCount;
    private TimeSpan _elapsed;

    public MainViewModel(Dispatcher dispatcher, SettingsService? settingsService = null)
    {
        _dispatcher = dispatcher;
        _settingsService = settingsService ?? new SettingsService();
        Settings = _settingsService.Load();
        LocalizationService.Apply(Settings.Language);
        _appliedLanguage = Settings.Language;
        ByteSizeConverter.CurrentMode = Settings.FileSizeDisplay;
        SizeFilters = CreateSizeFilters();
        CategoryFilters = CreateCategoryFilters();
        ThemeModes = CreateThemeModes();
        SizeDisplayModes = CreateSizeDisplayModes();
        _selectedSizeFilter = SizeFilters[0];
        _selectedCategoryFilter = CategoryFilters[0];
        FilesView = CollectionViewSource.GetDefaultView(Files);
        FilesView.Filter = FilterFile;
        FilesView.SortDescriptions.Add(new(nameof(FileItem.Size), ListSortDirection.Descending));
        _searchDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (sender, _) =>
        {
            if (sender is DispatcherTimer timer) timer.Stop();
            FilesView.Refresh();
            OnPropertyChanged(nameof(VisibleCountText));
        }, dispatcher);
        RefreshDrives();
    }

    public ObservableCollection<DriveSnapshot> Drives { get; } = new();
    public ObservableCollection<FileItem> Files { get; } = new();
    public ObservableCollection<FolderSummary> Folders { get; } = new();
    public ICollectionView FilesView { get; }
    public IReadOnlyList<SizeFilterOption> SizeFilters { get; private set; }
    public IReadOnlyList<CategoryFilterOption> CategoryFilters { get; private set; }
    public IReadOnlyList<ThemeModeOption> ThemeModes { get; private set; }
    public IReadOnlyList<SizeDisplayModeOption> SizeDisplayModes { get; private set; }
    public IReadOnlyList<LanguageModeOption> LanguageModes { get; } =
    [
        new("Deutsch", AppLanguage.German),
        new("English", AppLanguage.English)
    ];
    public AppSettings Settings { get; }
    public string VersionText => LocalizationService.Format(
        "VersionFormat",
        typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    public AppPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!SetProperty(ref _currentPage, value)) return;
            OnPropertyChanged(nameof(IsOverviewPage));
            OnPropertyChanged(nameof(IsResultsPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(IsAboutPage));
        }
    }

    public bool IsOverviewPage => CurrentPage == AppPage.Overview;
    public bool IsResultsPage => CurrentPage == AppPage.Results;
    public bool IsSettingsPage => CurrentPage == AppPage.Settings;
    public bool IsAboutPage => CurrentPage == AppPage.About;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value)) return;
            OnPropertyChanged(nameof(CanStartScan));
            OnPropertyChanged(nameof(CanCancelScan));
            OnPropertyChanged(nameof(CanRecycleSelectedFile));
        }
    }

    public bool CanStartScan => !IsScanning;
    public bool CanCancelScan => IsScanning;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
    }

    public SizeFilterOption SelectedSizeFilter
    {
        get => _selectedSizeFilter;
        set
        {
            if (!SetProperty(ref _selectedSizeFilter, value ?? SizeFilters[0])) return;
            FilesView.Refresh();
            OnPropertyChanged(nameof(VisibleCountText));
        }
    }

    public CategoryFilterOption SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (!SetProperty(ref _selectedCategoryFilter, value ?? CategoryFilters[0])) return;
            FilesView.Refresh();
            OnPropertyChanged(nameof(VisibleCountText));
        }
    }

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetProperty(ref _selectedFile, value)) return;
            OnPropertyChanged(nameof(HasSelectedFile));
            OnPropertyChanged(nameof(CanRecycleSelectedFile));
        }
    }

    public bool HasSelectedFile => SelectedFile is not null;
    public bool CanRecycleSelectedFile => !IsScanning && SelectedFile is { Safety.DeletionBlocked: false };

    public FolderSummary? SelectedFolder
    {
        get => _selectedFolder;
        set => SetProperty(ref _selectedFolder, value);
    }

    public string StatusHeading => LocalizationService.Get(_scanStatus switch
    {
        ScanStatus.Scanning => "ScanningHeading",
        ScanStatus.Cancelled => "CancelledHeading",
        ScanStatus.Complete => "CompleteHeading",
        ScanStatus.Failed => "FailedHeading",
        _ => "ReadyHeading"
    });

    public string StatusDetail => _scanStatus switch
    {
        ScanStatus.Scanning when _progressPath.Length > 0 => LocalizationService.Format("ScanningPath", _progressPath),
        ScanStatus.Scanning => LocalizationService.Get("ScanningInitial"),
        ScanStatus.Cancelled => LocalizationService.Format("CancelledDetail", FileCountText),
        ScanStatus.Complete => LocalizationService.Format("CompleteDetail", FileCountText, TotalSizeText, ElapsedText),
        ScanStatus.Failed => LocalizationService.Get("FailedDetail"),
        _ => LocalizationService.Get("ReadyDetail")
    };

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string FileCountText => _fileCount.ToString("N0", CultureInfo.CurrentCulture);
    public string TotalSizeText => ByteSizeFormatter.Format(_totalBytes, GetSizePreference());
    public string FolderCountText => _folderCount.ToString("N0", CultureInfo.CurrentCulture);
    public string SkippedCountText => _skippedCount.ToString("N0", CultureInfo.CurrentCulture);
    public string ElapsedText => _elapsed.TotalHours >= 1
        ? _elapsed.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : _elapsed.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    public string VisibleCountText => LocalizationService.Format(
        "ShownCount",
        (FilesView is CollectionView view ? view.Count : Files.Count).ToString("N0", CultureInfo.CurrentCulture));

    public void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in DriveService.GetDrives()) Drives.Add(drive);
    }

    public async Task StartScanAsync(string path)
    {
        if (IsScanning) return;

        _scanCancellation?.Dispose();
        var scanCancellation = new CancellationTokenSource();
        _scanCancellation = scanCancellation;
        var cancellationToken = scanCancellation.Token;

        Files.Clear();
        FilesView.SortDescriptions.Clear();
        Folders.Clear();
        SelectedFile = null;
        _fileCount = _totalBytes = _folderCount = _skippedCount = 0;
        _elapsed = TimeSpan.Zero;
        NotifyMetrics();
        CurrentPath = Path.GetFullPath(path);
        CurrentPage = AppPage.Results;
        IsScanning = true;
        _progressPath = string.Empty;
        SetScanStatus(ScanStatus.Scanning);

        var progress = new Progress<ScanProgress>(UpdateProgress);
        try
        {
            var options = new ScanOptions(
                Settings.IncludeHiddenFiles,
                Settings.IncludeSystemFiles,
                BatchSize: 1000);

            var result = await Task.Run(() => _scanner.ScanAsync(
                CurrentPath,
                options,
                AddBatchAsync,
                progress,
                issue => Interlocked.Increment(ref _skippedCount),
                cancellationToken));

            foreach (var folder in result.LargestFolders) Folders.Add(folder);
            _fileCount = result.FileCount;
            _totalBytes = result.TotalBytes;
            _folderCount = result.FolderCount;
            _skippedCount = Math.Max(_skippedCount, result.SkippedItems);
            _elapsed = result.Duration;
            FilesView.SortDescriptions.Clear();
            FilesView.SortDescriptions.Add(new(nameof(FileItem.Size), ListSortDirection.Descending));
            NotifyMetrics();

            SetScanStatus(result.WasCancelled ? ScanStatus.Cancelled : ScanStatus.Complete);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or NotSupportedException)
        {
            SetScanStatus(ScanStatus.Failed);
        }
        finally
        {
            IsScanning = false;
            if (ReferenceEquals(_scanCancellation, scanCancellation)) _scanCancellation = null;
            scanCancellation.Dispose();
            OnPropertyChanged(nameof(VisibleCountText));
        }
    }

    public void CancelScan() => _scanCancellation?.Cancel();

    public void RemoveFile(FileItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!Files.Remove(item)) return;

        if (SelectedFile == item) SelectedFile = null;
        _fileCount = Math.Max(0, _fileCount - 1);
        _totalBytes = Math.Max(0, _totalBytes - item.Size);

        for (var index = 0; index < Folders.Count; index++)
        {
            var folder = Folders[index];
            if (!IsWithin(item.DirectoryPath, folder.FullPath)) continue;

            var updated = folder with
            {
                Size = Math.Max(0, folder.Size - item.Size),
                FileCount = Math.Max(0, folder.FileCount - 1)
            };
            Folders[index] = updated;
            if (SelectedFolder == folder) SelectedFolder = updated;
        }

        FilesView.Refresh();
        CollectionViewSource.GetDefaultView(Folders).Refresh();
        NotifyMetrics();
        OnPropertyChanged(nameof(VisibleCountText));
    }

    public void SaveSettings()
    {
        _settingsService.Save(Settings);
        if (_appliedLanguage != Settings.Language)
        {
            _appliedLanguage = Settings.Language;
            LocalizationService.Apply(Settings.Language);
            RefreshLocalization();
        }

        ThemeService.Apply(Settings.Theme);
        ByteSizeConverter.CurrentMode = Settings.FileSizeDisplay;
        FilesView.Refresh();
        CollectionViewSource.GetDefaultView(Folders).Refresh();
        RefreshDrives();
        OnPropertyChanged(nameof(TotalSizeText));
    }

    private Task AddBatchAsync(IReadOnlyList<FileItem> batch) => _dispatcher.InvokeAsync(() =>
    {
        foreach (var item in batch) Files.Add(item);
        OnPropertyChanged(nameof(VisibleCountText));
    }, DispatcherPriority.Background).Task;

    private void UpdateProgress(ScanProgress progress)
    {
        _fileCount = progress.FilesFound;
        _totalBytes = progress.BytesFound;
        _folderCount = progress.FoldersVisited;
        _skippedCount = Math.Max(_skippedCount, progress.SkippedItems);
        _elapsed = progress.Elapsed;
        _progressPath = ShortenPath(progress.CurrentPath);
        OnPropertyChanged(nameof(StatusDetail));
        NotifyMetrics();
    }

    private bool FilterFile(object candidate)
    {
        if (candidate is not FileItem item) return false;
        if (item.Size < SelectedSizeFilter.MinimumBytes) return false;
        if (SelectedCategoryFilter.Category is { } category && item.FileCategory != category) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        return item.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || item.FullPath.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || item.Extension.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || LocalizationService.GetFileTypeName(item.Extension).Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshLocalization()
    {
        var selectedMinimum = SelectedSizeFilter.MinimumBytes;
        var selectedCategory = SelectedCategoryFilter.Category;

        SizeFilters = CreateSizeFilters();
        CategoryFilters = CreateCategoryFilters();
        ThemeModes = CreateThemeModes();
        SizeDisplayModes = CreateSizeDisplayModes();
        _selectedSizeFilter = SizeFilters.First(option => option.MinimumBytes == selectedMinimum);
        _selectedCategoryFilter = CategoryFilters.First(option => option.Category == selectedCategory);

        OnPropertyChanged(nameof(SizeFilters));
        OnPropertyChanged(nameof(CategoryFilters));
        OnPropertyChanged(nameof(ThemeModes));
        OnPropertyChanged(nameof(SizeDisplayModes));
        OnPropertyChanged(nameof(SelectedSizeFilter));
        OnPropertyChanged(nameof(SelectedCategoryFilter));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(StatusHeading));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(VisibleCountText));

        for (var index = 0; index < Files.Count; index++)
        {
            var item = Files[index];
            Files[index] = item;
        }

        for (var index = 0; index < Folders.Count; index++)
        {
            var folder = Folders[index];
            Folders[index] = folder;
        }
    }

    private static IReadOnlyList<SizeFilterOption> CreateSizeFilters() =>
    [
        new(LocalizationService.Get("FilterAnySize"), 0),
        new(LocalizationService.Get("FilterOver100Mb"), 100L * 1024 * 1024),
        new(LocalizationService.Get("FilterOver500Mb"), 500L * 1024 * 1024),
        new(LocalizationService.Get("FilterOver1Gb"), 1024L * 1024 * 1024),
        new(LocalizationService.Get("FilterOver5Gb"), 5L * 1024 * 1024 * 1024)
    ];

    private static IReadOnlyList<CategoryFilterOption> CreateCategoryFilters() =>
    [
        new(LocalizationService.Get("CategoryAll"), null),
        new(LocalizationService.Get("CategoryVideos"), FileCategory.Video),
        new(LocalizationService.Get("CategoryImages"), FileCategory.Image),
        new(LocalizationService.Get("CategoryArchives"), FileCategory.Archive),
        new(LocalizationService.Get("CategoryPrograms"), FileCategory.Program),
        new(LocalizationService.Get("CategoryDocuments"), FileCategory.Document),
        new(LocalizationService.Get("CategoryAudio"), FileCategory.Audio),
        new(LocalizationService.Get("CategoryOther"), FileCategory.Other)
    ];

    private static IReadOnlyList<ThemeModeOption> CreateThemeModes() =>
    [
        new(LocalizationService.Get("ThemeSystem"), ThemeMode.System),
        new(LocalizationService.Get("ThemeLight"), ThemeMode.Light),
        new(LocalizationService.Get("ThemeDark"), ThemeMode.Dark)
    ];

    private static IReadOnlyList<SizeDisplayModeOption> CreateSizeDisplayModes() =>
    [
        new(LocalizationService.Get("SizeAutomatic"), FileSizeDisplayMode.Automatic),
        new(LocalizationService.Get("SizeBinary"), FileSizeDisplayMode.Binary),
        new(LocalizationService.Get("SizeDecimal"), FileSizeDisplayMode.Decimal)
    ];

    private void SetScanStatus(ScanStatus status)
    {
        _scanStatus = status;
        OnPropertyChanged(nameof(StatusHeading));
        OnPropertyChanged(nameof(StatusDetail));
    }

    private static bool IsWithin(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyMetrics()
    {
        OnPropertyChanged(nameof(FileCountText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(FolderCountText));
        OnPropertyChanged(nameof(SkippedCountText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(StatusDetail));
    }

    private SizeUnitPreference GetSizePreference() => Settings.FileSizeDisplay switch
    {
        FileSizeDisplayMode.Decimal => SizeUnitPreference.Decimal,
        FileSizeDisplayMode.Binary => SizeUnitPreference.Binary,
        _ => SizeUnitPreference.Automatic
    };

    private static string ShortenPath(string path) => path.Length <= 88 ? path : $"…{path[^87..]}";
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DiskScope.Core.Models;
using DiskScope.Services;
using DiskScope.ViewModels;
using Microsoft.Win32;

namespace DiskScope;

public partial class MainWindow : Window
{
    private const string ProjectUrl = "https://github.com/pcalsys/DiskScope";
    private readonly FileActionService _fileActions = new();
    private readonly MainViewModel _viewModel;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(SettingsService? settingsService)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher, settingsService);
        DataContext = _viewModel;
        SourceInitialized += OnSourceInitialized;
        ThemeService.ThemeChanged += OnThemeChanged;
        Closing += (_, _) => _viewModel.CancelScan();
        Closed += OnClosed;
    }

    private void OverviewNav_Click(object sender, RoutedEventArgs e) => _viewModel.CurrentPage = AppPage.Overview;
    private void ResultsNav_Click(object sender, RoutedEventArgs e) => _viewModel.CurrentPage = AppPage.Results;
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => _viewModel.CurrentPage = AppPage.Settings;
    private void AboutNav_Click(object sender, RoutedEventArgs e) => _viewModel.CurrentPage = AppPage.About;

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.Get("ChooseFolderDialog"),
            Multiselect = false,
            InitialDirectory = Directory.Exists(_viewModel.CurrentPath)
                ? _viewModel.CurrentPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog(this) == true)
        {
            await StartScanSafelyAsync(dialog.FolderName);
        }
    }

    private async void ScanDrive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) await StartScanSafelyAsync(path);
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _viewModel.CancelScan();
    private void RefreshDrives_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshDrives();

    private void FilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetClickedRowItem<FileItem>(sender, e) is { } item) RunFileAction(item, _fileActions.OpenFile);
    }

    private void FoldersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetClickedRowItem<FolderSummary>(sender, e) is not { } folder) return;
        try
        {
            if (!Directory.Exists(folder.FullPath)) throw new DirectoryNotFoundException(LocalizationService.Get("FolderUnavailable"));
            Process.Start(new ProcessStartInfo(folder.FullPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (IsExpectedActionException(exception))
        {
            ShowActionError(exception);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } item) RunFileAction(item, _fileActions.OpenFile);
    }

    private void RevealFile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } item) RunFileAction(item, _fileActions.OpenContainingFolder);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is not { } item) return;
        try
        {
            Clipboard.SetText(item.FullPath);
        }
        catch (COMException exception)
        {
            ShowActionError(exception);
        }
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is not { } item) return;
        RunFileAction(item, file => _fileActions.ShowProperties(file.FullPath, new WindowInteropHelper(this).Handle));
    }

    private async void RecycleFile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is not { } item) return;
        if (_viewModel.IsScanning)
        {
            MessageBox.Show(this,
                LocalizationService.Get("ScanInProgressMessage"),
                LocalizationService.Get("ScanInProgressTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (item.Safety.DeletionBlocked)
        {
            MessageBox.Show(this,
                LocalizationService.Get("ProtectedSystemMessage"),
                LocalizationService.Get("ProtectedSystemTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_viewModel.Settings.ConfirmBeforeRecycle || item.Safety.RequiresElevatedWarning)
        {
            var warning = item.Safety.RequiresElevatedWarning
                ? LocalizationService.Get("HighRiskWarning")
                : string.Empty;
            var answer = MessageBox.Show(this,
                warning + LocalizationService.Format("RecycleQuestion", item.Name, item.FullPath),
                LocalizationService.Get("ConfirmFileAction"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        try
        {
            await _fileActions.MoveToRecycleBinAsync(item.FullPath);
            _viewModel.RemoveFile(item);
        }
        catch (Exception exception) when (IsExpectedActionException(exception) || exception is OperationCanceledException)
        {
            ShowActionError(exception);
        }
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel) return;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _viewModel.SaveSettings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ShowActionError(exception);
            }
        });
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => ApplyWindowTheme(ThemeService.IsDark);

    private void OnThemeChanged(bool useDark) => ApplyWindowTheme(useDark);

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        SourceInitialized -= OnSourceInitialized;
        Closed -= OnClosed;
    }

    private void ApplyWindowTheme(bool useDark)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var enabled = useDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
        }
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
        }
        catch (Exception exception) when (IsExpectedActionException(exception))
        {
            ShowActionError(exception);
        }
    }

    private async Task StartScanSafelyAsync(string path)
    {
        try
        {
            await _viewModel.StartScanAsync(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowActionError(exception);
        }
    }

    private void RunFileAction(FileItem item, Action<FileItem> action)
    {
        try
        {
            action(item);
        }
        catch (Exception exception) when (IsExpectedActionException(exception))
        {
            ShowActionError(exception);
        }
    }

    private void RunFileAction(FileItem item, Action<string> action)
    {
        try
        {
            action(item.FullPath);
        }
        catch (Exception exception) when (IsExpectedActionException(exception))
        {
            ShowActionError(exception);
        }
    }

    private void ShowActionError(Exception exception) => MessageBox.Show(this,
        exception.Message,
        LocalizationService.Get("ActionErrorTitle"),
        MessageBoxButton.OK,
        MessageBoxImage.Error);

    private static T? GetClickedRowItem<T>(object sender, MouseButtonEventArgs e) where T : class
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source) return null;
        return ItemsControl.ContainerFromElement(grid, source) is DataGridRow { DataContext: T item } ? item : null;
    }

    private static bool IsExpectedActionException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException;

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}

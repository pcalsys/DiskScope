using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiskScope.Core.Models;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;
using DiskScope.ViewModels;

namespace DiskScope.ScreenshotGenerator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine("Usage: DiskScope.ScreenshotGenerator <png-output> <light|dark> <overview|results|settings|about> <english|german>");
            return 2;
        }

        if (!Enum.TryParse<ThemeMode>(args[1], ignoreCase: true, out var theme) || theme == ThemeMode.System)
        {
            Console.Error.WriteLine("Theme must be either 'light' or 'dark'.");
            return 2;
        }

        if (!Enum.TryParse<AppPage>(args[2], ignoreCase: true, out var page))
        {
            Console.Error.WriteLine("Page must be overview, results, settings, or about.");
            return 2;
        }

        if (!Enum.TryParse<AppLanguage>(args[3], ignoreCase: true, out var language))
        {
            Console.Error.WriteLine("Language must be either 'english' or 'german'.");
            return 2;
        }

        var settingsDirectory = Path.Combine(Path.GetTempPath(), "DiskScope.ScreenshotGenerator", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(settingsDirectory, "settings.json");
        MainWindow? window = null;
        var application = CreateApplication();
        try
        {
            var settingsService = new SettingsService(settingsPath);
            settingsService.Save(new AppSettings { Theme = theme, Language = language });
            LocalizationService.Apply(language);
            ThemeService.Apply(theme);

            window = new MainWindow(settingsService)
            {
                Width = 1440,
                Height = 900,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false
            };
            PopulatePreview(window, page);
            window.Show();
            window.UpdateLayout();

            var client = (FrameworkElement)window.Content;
            var width = Math.Max(1, (int)Math.Ceiling(client.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(client.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(client);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var output = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using (var stream = File.Create(output)) encoder.Save(stream);

            Console.WriteLine($"Generated {language} {theme}-theme {page} render {output} ({width}x{height})");
            return 0;
        }
        finally
        {
            window?.Close();
            application.Shutdown();
            if (File.Exists(settingsPath)) File.Delete(settingsPath);
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory);
        }
    }

    private static Application CreateApplication()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new()
        {
            Source = new Uri("/DiskScope;component/Themes/Light.xaml", UriKind.Relative)
        });
        application.Resources.MergedDictionaries.Add(new()
        {
            Source = new Uri("/DiskScope;component/Themes/Controls.xaml", UriKind.Relative)
        });
        application.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        application.Resources["ByteSizeConverter"] = new ByteSizeConverter();
        application.Resources["DriveUsageConverter"] = new DriveUsageConverter();
        application.Resources["SafetyTextConverter"] = new SafetyTextConverter();
        application.Resources["FileTypeNameConverter"] = new FileTypeNameConverter();
        application.Resources["DriveTypeNameConverter"] = new DriveTypeNameConverter();
        return application;
    }

    private static void PopulatePreview(MainWindow window, AppPage page)
    {
        if (window.DataContext is not MainViewModel viewModel) return;

        viewModel.Drives.Clear();
        viewModel.Drives.Add(new("C:\\", "Windows (C:)", DriveType.Fixed, "NTFS", 1_000_204_886_016, 618_475_290_624, true));
        viewModel.Drives.Add(new("D:\\", "Projects (D:)", DriveType.Fixed, "NTFS", 2_000_398_934_016, 1_168_264_421_376, true));
        viewModel.Drives.Add(new("E:\\", "Backup (E:)", DriveType.Removable, "exFAT", 500_096_991_232, 352_296_435_712, true));

        var safety = new SafetyAssessment(
            SafetyCategory.Personal,
            "Personal file",
            "This file is in a user-content location and was likely created, downloaded, or saved by a person.",
            "Open or back up the file before removing it. DiskScope cannot guarantee that any file is safe to delete.",
            DeletionBlocked: false,
            RequiresElevatedWarning: false);
        var previewFile = new FileItem(
            "family-video.mp4",
            @"C:\Users\Sample\Videos\family-video.mp4",
            @"C:\Users\Sample\Videos",
            ".mp4",
            "MP4 file",
            FileCategory.Video,
            4_833_820_672,
            new DateTime(2026, 8, 20, 18, 30, 0),
            FileAttributes.Archive,
            safety);
        viewModel.Files.Add(previewFile);
        viewModel.Folders.Add(new("Videos", @"C:\Users\Sample\Videos", 12_884_901_888, 42));
        viewModel.SelectedFile = previewFile;
        viewModel.FilesView.Refresh();
        viewModel.CurrentPage = page;
    }
}

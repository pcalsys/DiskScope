using System.Windows;
using System.Windows.Threading;
using DiskScope.Services;

namespace DiskScope;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        LocalizationService.Apply(settings.Language);
        ThemeService.Initialize(settings.Theme);

        var mainWindow = new MainWindow(settingsService);
        if (e.Args.Contains("--ui-smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            mainWindow.ShowInTaskbar = false;
            mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            mainWindow.Left = -10000;
            mainWindow.Top = -10000;
            mainWindow.Show();
            mainWindow.UpdateLayout();
            mainWindow.Close();
            Shutdown(0);
            return;
        }

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.Stop();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            LocalizationService.Format("UnexpectedError", e.Exception.Message),
            "DiskScope",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }
}

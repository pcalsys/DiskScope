using System.IO;
using System.Windows;
using System.Windows.Threading;
using DiskScope.Models;
using Microsoft.Win32;

namespace DiskScope.Services;

public static class ThemeService
{
    private static ThemeMode _currentMode = ThemeMode.System;
    private static bool _isMonitoringWindowsTheme;

    public static bool IsDark { get; private set; }

    public static event Action<bool>? ThemeChanged;

    public static void Initialize(ThemeMode themeMode)
    {
        if (!_isMonitoringWindowsTheme)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _isMonitoringWindowsTheme = true;
        }

        Apply(themeMode);
    }

    public static void Apply(ThemeMode themeMode)
    {
        _currentMode = themeMode;
        var useDark = themeMode == ThemeMode.Dark || (themeMode == ThemeMode.System && IsSystemDark());
        ApplyResolvedTheme(useDark);
    }

    public static void Stop()
    {
        if (!_isMonitoringWindowsTheme) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _isMonitoringWindowsTheme = false;
    }

    private static void ApplyResolvedTheme(bool useDark)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var expectedTheme = useDark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var current = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) == true
            || dictionary.Source?.OriginalString.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (current?.Source?.OriginalString.EndsWith(expectedTheme, StringComparison.OrdinalIgnoreCase) == true)
        {
            IsDark = useDark;
            ThemeChanged?.Invoke(useDark);
            return;
        }

        var replacement = new ResourceDictionary
        {
            Source = new Uri($"/DiskScope;component/{expectedTheme}", UriKind.Relative)
        };

        if (current is null) dictionaries.Insert(0, replacement);
        else dictionaries[dictionaries.IndexOf(current)] = replacement;

        IsDark = useDark;
        ThemeChanged?.Invoke(useDark);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_currentMode != ThemeMode.System) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
        dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (_currentMode == ThemeMode.System) ApplyResolvedTheme(IsSystemDark());
        });
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}

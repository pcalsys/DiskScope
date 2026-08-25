using System.Globalization;

namespace DiskScope.Models;

public enum AppLanguage
{
    English,
    German
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public enum FileSizeDisplayMode
{
    Automatic,
    Binary,
    Decimal
}

public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
        ? AppLanguage.German
        : AppLanguage.English;
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool IncludeHiddenFiles { get; set; }
    public bool IncludeSystemFiles { get; set; }
    public bool ConfirmBeforeRecycle { get; set; } = true;
    public FileSizeDisplayMode FileSizeDisplay { get; set; } = FileSizeDisplayMode.Automatic;
}

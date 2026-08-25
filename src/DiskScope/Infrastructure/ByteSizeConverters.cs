using System.Globalization;
using System.Windows.Data;
using DiskScope.Core;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.Infrastructure;

public sealed class ByteSizeConverter : IValueConverter
{
    public static FileSizeDisplayMode CurrentMode { get; set; } = FileSizeDisplayMode.Automatic;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is long bytes
        ? ByteSizeFormatter.Format(bytes, GetPreference())
        : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    public static string Format(long bytes) => ByteSizeFormatter.Format(bytes, GetPreference());

    private static SizeUnitPreference GetPreference() => CurrentMode switch
    {
        FileSizeDisplayMode.Binary => SizeUnitPreference.Binary,
        FileSizeDisplayMode.Decimal => SizeUnitPreference.Decimal,
        _ => SizeUnitPreference.Automatic
    };
}

public sealed class DriveUsageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length < 2 || values[0] is not long used || values[1] is not long total)
        {
            return LocalizationService.Get("DriveNotReady");
        }

        return LocalizationService.Format("DriveUsage", ByteSizeConverter.Format(used), ByteSizeConverter.Format(total));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

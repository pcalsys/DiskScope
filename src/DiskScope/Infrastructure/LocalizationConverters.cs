using System.Globalization;
using System.IO;
using System.Windows.Data;
using DiskScope.Core.Models;
using DiskScope.Services;

namespace DiskScope.Infrastructure;

public sealed class SafetyTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SafetyAssessment assessment && parameter is string part
            ? LocalizationService.GetSafetyText(assessment, part)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class FileTypeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        LocalizationService.GetFileTypeName(value as string ?? string.Empty);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class DriveTypeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is DriveType driveType
        ? LocalizationService.Get(driveType switch
        {
            DriveType.Fixed => "DriveFixed",
            DriveType.Removable => "DriveRemovable",
            DriveType.Network => "DriveNetwork",
            DriveType.CDRom => "DriveCdRom",
            DriveType.Ram => "DriveRam",
            _ => "DriveUnknown"
        })
        : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

using System.Globalization;

namespace DiskScope.Core;

public enum SizeUnitPreference
{
    Automatic,
    Binary,
    Decimal
}

public static class ByteSizeFormatter
{
    public static string Format(long bytes, SizeUnitPreference preference = SizeUnitPreference.Automatic)
    {
        var divisor = preference == SizeUnitPreference.Decimal ? 1000d : 1024d;
        var units = preference == SizeUnitPreference.Binary
            ? new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB" }
            : new[] { "B", "KB", "MB", "GB", "TB", "PB" };

        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= divisor && unit < units.Length - 1)
        {
            value /= divisor;
            unit++;
        }

        var format = unit == 0 ? "0" : value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return string.Create(CultureInfo.CurrentCulture, $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unit]}");
    }
}

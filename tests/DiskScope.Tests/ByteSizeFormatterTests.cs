using DiskScope.Core;

namespace DiskScope.Tests;

public sealed class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0, "B")]
    [InlineData(1024, "KB")]
    [InlineData(1048576, "MB")]
    [InlineData(1073741824, "GB")]
    public void Format_SelectsExpectedUnit(long bytes, string expectedUnit)
    {
        var formatted = ByteSizeFormatter.Format(bytes);
        Assert.EndsWith(expectedUnit, formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesDecimalScalingWhenRequested()
    {
        var formatted = ByteSizeFormatter.Format(1000, SizeUnitPreference.Decimal);
        Assert.EndsWith("KB", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("1000", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesIecLabelsForBinaryPreference()
    {
        Assert.EndsWith("KiB", ByteSizeFormatter.Format(1024, SizeUnitPreference.Binary), StringComparison.Ordinal);
    }
}

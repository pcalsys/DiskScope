using DiskScope.Core.Models;
using DiskScope.Core.Services;

namespace DiskScope.Tests;

public sealed class SafetyClassifierTests
{
    private readonly SafetyClassifier _classifier = new(
        windowsPath: @"C:\Windows",
        programPaths: new[] { @"C:\Program Files", @"C:\Program Files (x86)" },
        programDataPath: @"C:\ProgramData",
        userProfilePath: @"C:\Users\Alice",
        localAppDataPath: @"C:\Users\Alice\AppData\Local",
        roamingAppDataPath: @"C:\Users\Alice\AppData\Roaming",
        tempPath: @"C:\Users\Alice\AppData\Local\Temp");

    [Fact]
    public void Assess_BlocksCriticalWindowsFiles()
    {
        var assessment = _classifier.Assess(@"C:\Windows\System32\config\SYSTEM", FileCategory.Other);
        Assert.Equal(SafetyCategory.CriticalSystem, assessment.Category);
        Assert.True(assessment.DeletionBlocked);
    }

    [Fact]
    public void Assess_BlocksOtherWindowsFiles()
    {
        var assessment = _classifier.Assess(@"C:\Windows\Fonts\segoeui.ttf", FileCategory.Other);
        Assert.Equal(SafetyCategory.WindowsSystem, assessment.Category);
        Assert.True(assessment.DeletionBlocked);
    }

    [Theory]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\swapfile.sys")]
    [InlineData(@"D:\pagefile.sys")]
    [InlineData(@"C:\Boot\BCD")]
    [InlineData(@"C:\Recovery\WindowsRE\Winre.wim")]
    [InlineData(@"C:\System Volume Information\tracking.log")]
    [InlineData(@"D:\$Recycle.Bin\S-1-5-21\$R1.txt")]
    public void Assess_BlocksProtectedRootAndVolumeFiles(string path)
    {
        var assessment = _classifier.Assess(path, FileCategory.Other);
        Assert.Equal(SafetyCategory.CriticalSystem, assessment.Category);
        Assert.True(assessment.DeletionBlocked);
    }

    [Theory]
    [InlineData(@"C:\Windows.old\Windows\explorer.exe")]
    [InlineData(@"C:\$WINDOWS.~BT\Sources\install.esd")]
    [InlineData(@"C:\$WinREAgent\Scratch\update.bin")]
    public void Assess_BlocksWindowsMaintenanceLocations(string path)
    {
        var assessment = _classifier.Assess(path, FileCategory.Other);
        Assert.Equal(SafetyCategory.WindowsSystem, assessment.Category);
        Assert.True(assessment.DeletionBlocked);
    }

    [Fact]
    public void Assess_DoesNotProtectSimilarNamesOutsideSystemLocations()
    {
        var assessment = _classifier.Assess(@"D:\Downloads\pagefile.sys", FileCategory.Other);
        Assert.Equal(SafetyCategory.Unknown, assessment.Category);
        Assert.False(assessment.DeletionBlocked);
    }

    [Fact]
    public void Assess_AdvisesUninstallForProgramFiles()
    {
        var assessment = _classifier.Assess(@"C:\Program Files\Example\example.dll", FileCategory.Program);
        Assert.Equal(SafetyCategory.ProgramFile, assessment.Category);
        Assert.Contains("Uninstall", assessment.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.False(assessment.DeletionBlocked);
    }

    [Theory]
    [InlineData(@"C:\Users\Alice\Downloads\video.mp4", SafetyCategory.Personal)]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\cache.bin", SafetyCategory.Temporary)]
    [InlineData(@"C:\Users\Alice\AppData\Roaming\Example\state.db", SafetyCategory.ApplicationData)]
    [InlineData(@"D:\unclassified\blob.bin", SafetyCategory.Unknown)]
    public void Assess_UsesConservativeLocationCategories(string path, SafetyCategory expected)
    {
        Assert.Equal(expected, _classifier.Assess(path, FileCategory.Other).Category);
    }
}

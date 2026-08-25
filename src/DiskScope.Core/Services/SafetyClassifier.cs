using DiskScope.Core.Models;

namespace DiskScope.Core.Services;

public sealed class SafetyClassifier
{
    private static readonly SafetyAssessment CriticalSystemAssessment = new(
        SafetyCategory.CriticalSystem, "Critical system file",
        "This file appears to be part of Windows startup, the registry, or another critical operating-system component.",
        "Do not move or delete this file. Use Windows recovery or servicing tools if it is damaged.", true, true);
    private static readonly SafetyAssessment WindowsSystemAssessment = new(
        SafetyCategory.WindowsSystem, "Windows system file",
        "This file is stored inside the Windows directory and may be required by the operating system or a Windows feature.",
        "Do not delete it manually. Use Windows Settings, Disk Cleanup, or DISM for supported maintenance.", true, true);
    private static readonly SafetyAssessment InstalledProgramAssessment = new(
        SafetyCategory.ProgramFile, "Program file",
        "This file is stored in an installed-program directory and probably belongs to an application.",
        "Uninstall or modify the application through Windows Settings instead of deleting individual files.", false, true);
    private static readonly SafetyAssessment TemporaryAssessment = new(
        SafetyCategory.Temporary, "Temporary file",
        "This file is in a location commonly used for temporary data, but an application may still be using it.",
        "Close related applications first. Prefer Windows Storage cleanup and review the file before removal.", false, false);
    private static readonly SafetyAssessment ApplicationDataAssessment = new(
        SafetyCategory.ApplicationData, "Application data",
        "This file likely stores application settings, caches, databases, or user state.",
        "Remove it only when you understand which application created it. Uninstalling or resetting the app is usually safer.", false, true);
    private static readonly SafetyAssessment PersonalAssessment = new(
        SafetyCategory.Personal, "Personal file",
        "This file is in a user-content location and was likely created, downloaded, or saved by a person.",
        "Open or back up the file before removing it. DiskScope cannot guarantee that any file is safe to delete.", false, false);
    private static readonly SafetyAssessment ExecutableAssessment = new(
        SafetyCategory.ProgramFile, "Program-related file",
        "The file type can contain executable code or software components, but its owner could not be identified from its location.",
        "Do not delete it unless you know which program uses it. Prefer the program's uninstaller when applicable.", false, true);
    private static readonly SafetyAssessment UnknownAssessment = new(
        SafetyCategory.Unknown, "Unknown file",
        "DiskScope cannot reliably determine who created this file or whether another program depends on it.",
        "Inspect the file and its containing folder before taking action. Keep a backup if you are uncertain.", false, true);

    private static readonly HashSet<string> CriticalFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bootmgr", "bootmgfw.efi", "bcd", "ntldr", "ntdetect.com",
        "ntoskrnl.exe", "ntkrnlmp.exe", "hal.dll", "winload.exe", "winload.efi",
        "winresume.exe", "winresume.efi", "smss.exe", "csrss.exe", "wininit.exe",
        "services.exe", "lsass.exe", "registry", "system", "software", "sam", "security", "default"
    };
    private static readonly HashSet<string> CriticalSystemRootFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bootmgr", "bootnxt", "bootsect.bak", "hiberfil.sys", "pagefile.sys", "swapfile.sys",
        "ntldr", "ntdetect.com", "ntbootdd.sys", "io.sys", "msdos.sys"
    };
    private static readonly HashSet<string> CriticalSystemRootDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boot", "EFI", "Recovery"
    };
    private static readonly HashSet<string> ProtectedVolumeDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "System Volume Information"
    };
    private static readonly HashSet<string> WindowsMaintenanceDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$WINDOWS.~BT", "$WINDOWS.~WS", "$WinREAgent", "Windows.old"
    };

    private readonly string _windowsPath;
    private readonly string[] _programPaths;
    private readonly string _programDataPath;
    private readonly string _userProfilePath;
    private readonly string _localAppDataPath;
    private readonly string _roamingAppDataPath;
    private readonly string _tempPath;

    public SafetyClassifier(
        string? windowsPath = null,
        IEnumerable<string>? programPaths = null,
        string? programDataPath = null,
        string? userProfilePath = null,
        string? localAppDataPath = null,
        string? roamingAppDataPath = null,
        string? tempPath = null)
    {
        _windowsPath = Normalize(windowsPath ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        _programPaths = (programPaths ?? GetDefaultProgramPaths()).Select(Normalize).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _programDataPath = Normalize(programDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        _userProfilePath = Normalize(userProfilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        _localAppDataPath = Normalize(localAppDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        _roamingAppDataPath = Normalize(roamingAppDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        _tempPath = Normalize(tempPath ?? Path.GetTempPath());
    }

    public SafetyAssessment Assess(string fullPath, FileCategory fileCategory)
    {
        var path = Normalize(fullPath);
        var fileName = Path.GetFileName(path);

        if (IsCriticalWindowsPath(path, fileName))
        {
            return CriticalSystemAssessment;
        }

        if (IsWithin(path, _windowsPath) || IsWithinVolumeDirectory(path, WindowsMaintenanceDirectoryNames))
        {
            return WindowsSystemAssessment;
        }

        if (_programPaths.Any(programPath => IsWithin(path, programPath)))
        {
            return InstalledProgramAssessment;
        }

        if (IsWithin(path, _tempPath) || ContainsDirectory(path, "Temp") || ContainsDirectory(path, "Temporary Internet Files"))
        {
            return TemporaryAssessment;
        }

        if (IsWithin(path, _localAppDataPath) || IsWithin(path, _roamingAppDataPath) || IsWithin(path, _programDataPath))
        {
            return ApplicationDataAssessment;
        }

        if (IsLikelyPersonal(path))
        {
            return PersonalAssessment;
        }

        if (fileCategory == FileCategory.Program)
        {
            return ExecutableAssessment;
        }

        return UnknownAssessment;
    }

    private bool IsCriticalWindowsPath(string path, string fileName)
    {
        var volumeRoot = Normalize(Path.GetPathRoot(path) ?? string.Empty);
        if (IsFileDirectlyUnder(path, volumeRoot) && CriticalSystemRootFileNames.Contains(fileName)) return true;
        if (IsWithinTopLevelDirectory(path, volumeRoot, CriticalSystemRootDirectoryNames)) return true;
        if (IsWithinTopLevelDirectory(path, volumeRoot, ProtectedVolumeDirectoryNames)) return true;

        if (!IsWithin(path, _windowsPath)) return false;

        var system32 = Path.Combine(_windowsPath, "System32");
        var config = Path.Combine(system32, "config");
        var boot = Path.Combine(_windowsPath, "Boot");
        var winsxs = Path.Combine(_windowsPath, "WinSxS");

        return IsWithin(path, config)
            || IsWithin(path, boot)
            || IsWithin(path, winsxs)
            || (IsWithin(path, system32) && CriticalFileNames.Contains(fileName));
    }

    private static bool IsFileDirectlyUnder(string path, string directory) =>
        !string.IsNullOrWhiteSpace(directory)
        && Normalize(Path.GetDirectoryName(path) ?? string.Empty).Equals(directory, StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinTopLevelDirectory(string path, string root, HashSet<string> directoryNames)
    {
        if (!IsWithin(path, root) || path.Equals(root, StringComparison.OrdinalIgnoreCase)) return false;
        var relative = Path.GetRelativePath(root, path);
        var firstPart = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return directoryNames.Contains(firstPart);
    }

    private static bool IsWithinVolumeDirectory(string path, HashSet<string> directoryNames) =>
        IsWithinTopLevelDirectory(path, Normalize(Path.GetPathRoot(path) ?? string.Empty), directoryNames);

    private bool IsLikelyPersonal(string path)
    {
        if (!IsWithin(path, _userProfilePath)) return false;
        var relative = Path.GetRelativePath(_userProfilePath, path);
        var firstPart = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return firstPart.Equals("Desktop", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("Documents", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("Downloads", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("Pictures", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("Music", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("Videos", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("OneDrive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsDirectory(string path, string directoryName)
    {
        var surrounded = $"{Path.DirectorySeparatorChar}{directoryName}{Path.DirectorySeparatorChar}";
        return path.Contains(surrounded, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithin(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return Path.TrimEndingDirectorySeparator(path);
        }
    }

    private static IEnumerable<string> GetDefaultProgramPaths()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    }
}

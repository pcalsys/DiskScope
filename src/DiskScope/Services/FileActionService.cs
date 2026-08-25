using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace DiskScope.Services;

public sealed class FileActionService
{
    public void OpenFile(string path)
    {
        EnsureFileExists(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void OpenContainingFolder(string path)
    {
        EnsureFileExists(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    public void ShowProperties(string path, nint ownerHandle)
    {
        EnsureFileExists(path);
        if (!SHObjectProperties(ownerHandle, 2, path, null))
        {
            throw new InvalidOperationException(LocalizationService.Get("PropertiesError"));
        }
    }

    public Task MoveToRecycleBinAsync(string path)
    {
        EnsureFileExists(path);
        EnsurePathContainsNoReparsePoints(path);
        EnsureLocalRecycleBin(path);
        return Task.Run(() => FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException));
    }

    private static void EnsureFileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException(LocalizationService.Get("FileUnavailable"), path);
    }

    private static void EnsureLocalRecycleBin(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root) || new DriveInfo(root).DriveType != DriveType.Fixed)
        {
            throw new NotSupportedException(
                LocalizationService.Get("RecycleUnavailable"));
        }
    }

    private static void EnsurePathContainsNoReparsePoints(string path)
    {
        for (DirectoryInfo? directory = new FileInfo(path).Directory; directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new NotSupportedException(
                    LocalizationService.Get("ReparseRecycleBlocked"));
            }
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(nint hwnd, int shopObjectType, string pszObjectName, string? pszPropertyPage);
}

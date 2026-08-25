using DiskScope.Core.Models;

namespace DiskScope.Core.Services;

public static class DriveService
{
    public static IReadOnlyList<DriveSnapshot> GetDrives()
    {
        var drives = new List<DriveSnapshot>();
        foreach (var drive in DriveInfo.GetDrives().OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var ready = drive.IsReady;
                var label = ready && !string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})"
                    : drive.Name.TrimEnd('\\');
                drives.Add(new(
                    drive.Name,
                    label,
                    drive.DriveType,
                    ready ? drive.DriveFormat : string.Empty,
                    ready ? drive.TotalSize : 0,
                    ready ? drive.TotalFreeSpace : 0,
                    ready));
            }
            catch (IOException)
            {
                drives.Add(new(drive.Name, drive.Name.TrimEnd('\\'), drive.DriveType, string.Empty, 0, 0, false));
            }
            catch (UnauthorizedAccessException)
            {
                drives.Add(new(drive.Name, drive.Name.TrimEnd('\\'), drive.DriveType, string.Empty, 0, 0, false));
            }
        }

        return drives;
    }
}

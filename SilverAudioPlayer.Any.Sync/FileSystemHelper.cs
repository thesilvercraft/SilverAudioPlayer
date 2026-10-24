using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Humanizer;

static class FileSystemHelper
{
    public static string InvalidPathCharactersForFormat(string format) => InvalidCharactersForFormat(format);
    public static string InvalidFileCharactersForFormat(string format) => InvalidCharactersForFormat(format);

    private static string BuildWindowsInvalidChars()
    {
        var chars = new char[9 + 32];
        "<>:\"/\\|?*".CopyTo(chars);
        for (var i = 0; i < 32; i++)
        {
            chars[9 + i] = (char)i;
        }
        return new string(chars);
    }
    private static readonly string WindowsInvalidChars = BuildWindowsInvalidChars();
    private const string PosixInvalidChars = "/\0";

    private static string InvalidCharactersForFormat(string format)
    {
        var normalized = format.ToLowerInvariant();
        return normalized is "ntfs" or "exfat" or "fat" or "vfat" or "fuseblk" or "unknown" or ""
            ? WindowsInvalidChars
            : PosixInvalidChars;
    }

    public static string Clean(string i, string notallowed)
    {
        i = notallowed.Aggregate(i, (current, invalid) => current.Replace(invalid, '_'));
        i = i.TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(i)) return "_";

        var deviceName = i.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (deviceName is "CON" or "PRN" or "AUX" or "NUL" ||
            deviceName.Length == 4 &&
            (deviceName.StartsWith("COM", StringComparison.Ordinal) || deviceName.StartsWith("LPT", StringComparison.Ordinal)) &&
            deviceName[3] is >= '1' and <= '9')
            return "_" + i;

        return i;
    }


    [SupportedOSPlatform("windows")]
    public static void RefreshDevicesWindows(Action<IEnumerable<Device>> ret)
    {
        var collection = new ObservableCollection<Device>();
        ret.Invoke(collection);
        var driveInfos = DriveInfo.GetDrives();
        foreach (var queryObj in driveInfos)
        {
            Device device = new()
            {
                Model = queryObj.VolumeLabel,
                MountPoint = queryObj.RootDirectory.FullName,
                Type = queryObj.DriveType,
                Usage = queryObj.TotalSize - queryObj.TotalFreeSpace,
                Size = queryObj.TotalSize,
                Format = queryObj.DriveFormat,
            };
            collection.Add(device);
        }

    }
    public static string[] BlockList = ["/boot/efi", "/boot", "/efi"];

    [SupportedOSPlatform("linux")]

    public static void RefreshDevicesLinux(Action<IEnumerable<Device>> ret)
    {
        var mtab = File.ReadAllLines("/etc/mtab");
        var collection = new ObservableCollection<Device>();
        ret.Invoke(collection);
        var driveInfos = DriveInfo.GetDrives();

        foreach (var mount in mtab)
        {
            if (!mount.StartsWith("/dev/sd") && !mount.StartsWith("/dev/nvme") &&
                !mount.StartsWith("tmpfs /tmp")) continue;
            var dev = mount.Split(" ");
            if (BlockList.Contains(dev[1])) continue;
            var model = "Unknown";
            var removable = false;
            if (!mount.StartsWith("tmpfs /tmp"))
            {
                var devn = dev[0].Split("/").Last();
                for (var x = devn.Length - 1; x > 0; x--)
                {
                    if (char.IsDigit(devn[x])) continue;
                    if (devn.StartsWith("nvme") && devn[x] == 'p')
                    {
                        devn = devn[..x];
                    }
                    else
                    {
                        devn = devn[..(x + 1)];
                    }
                    break;
                }
                var a = File.ReadAllText($"/sys/block/{devn}/removable");

                removable = a == "1\n";
                model = File.ReadAllText($"/sys/block/{devn}/device/model");
            }

            Device device = new()
            {
                Model = model,
                MountPoint = dev[1],
                Type = removable ? DriveType.Removable : DriveType.Unknown,
                Format = dev[2]
            };

            var d = driveInfos.FirstOrDefault(x => x.Name == dev[1]);
            if (d != null)
            {
                device.Usage = d.TotalSize - d.TotalFreeSpace;
                device.Size = d.TotalSize;
                if (!removable)
                {
                    device.Type = d.DriveType;
                }
            }
            collection.Add(device);
        }
    }
}


public class Device
{
    public string Model;
    public string MountPoint;
    public DriveType Type;
    public long Size = 0;
    public long Usage = 0;
    public string Format;
    public override string ToString()
    {
        return $"{Model} {MountPoint} {Type} {Format} {Usage.Bytes()}/{Size.Bytes()}";
    }
}

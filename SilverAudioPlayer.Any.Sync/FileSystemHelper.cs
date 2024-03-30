using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Humanizer;

static class FileSystemHelper
{
    public static string InvalidPathCharactersForFormat(string format)
    {
        return format.ToLower() switch
        {
            "exfat" or "fat" or "vfat" => "*?.,;:/\\|+=<>[]\"\0",
            "ntfs" or "fuseblk" => "/\0",
            "btrfs" or "ext4" or "ext3" => "\0",
            _ => "*?.,;:/\\|+=<>[]\"\0"
        };
    }
    public static string InvalidFileCharactersForFormat(string format)
    {
        return format.ToLower() switch
        {
            "exfat" or "fat" or "vfat" => "*?,;:/\\|+=<>[]\"\0",
            "ntfs" or "fuseblk" => "/\0",
            "btrfs" or "ext4" or "ext3" => "\0",
            _ => "*?,;:/\\|+=<>[]\"\0"
        };
    }
    public static string Clean(string i, string notallowed)
    {
        foreach (var invalid in notallowed)
        {
            i = i.Replace(invalid, '_');
        }

        return i;
    }


    [SupportedOSPlatform("windows")]
    public static void RefreshDevicesWindows(Action<IEnumerable<Device>> ret)
    {
        var collection = new ObservableCollection<Device>();
        ret.Invoke(collection);
        var driveinfo = DriveInfo.GetDrives();
        foreach (var queryObj in driveinfo)
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
        var driveinfo = DriveInfo.GetDrives();

        foreach (var mount in mtab)
        {
            if (mount.StartsWith("/dev/sd") || mount.StartsWith("/dev/nvme") || mount.StartsWith("tmpfs /tmp"))
            {
                var dev = mount.Split(" ");
                if (!BlockList.Contains(dev[1]))
                {
                    string model = "Unknown";
                    bool removable = false;
                    if (!mount.StartsWith("tmpfs /tmp"))
                    {
                        var devn = dev[0].Split("/").Last();
                        for (int x = devn.Length - 1; x > 0; x--)
                        {
                            if (!char.IsDigit(devn[x]))
                            {
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

                    var d = driveinfo.FirstOrDefault(x => x.Name == dev[1]);
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
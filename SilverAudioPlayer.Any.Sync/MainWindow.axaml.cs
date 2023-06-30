using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using Humanizer;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.Sync;

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
public partial class MainWindow : Window
{
    public MainWindow()
    {

        InitializeComponent();
        DeviceBox = this.FindControl<ComboBox>("DeviceBox");
        TextBox = this.FindControl<TextEditor>("TextBox");
        RefreshDevices();
        if (WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks") == true)
        {
            return;
        }
        this.DoAfterInitTasks(true);
        Closing += (e, o) => { o.Cancel = Lock; };
    }

    public bool Lock = false;

    private ISyncEnvironmentListener env;

    public MainWindow(ISyncEnvironmentListener env) : this()
    {
        this.env = env;
    }

    public string[] BlockList = new[] { "/boot/efi", "/boot" };
    public void RefreshDevices()
    {
        if (OperatingSystem.IsWindows())
        {
            RefreshDevicesWindows();
        }
        else if (OperatingSystem.IsLinux())
        {
            RefreshDevicesLinux();
        }
    }



    [SupportedOSPlatform("windows")]
    public void RefreshDevicesWindows()
    {
        var collection = new ObservableCollection<Device>();
        DeviceBox.ItemsSource = collection;
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
    [SupportedOSPlatform("linux")]

    public void RefreshDevicesLinux()
    {
        var mtab = File.ReadAllLines("/etc/mtab");
        var collection = new ObservableCollection<Device>();
        DeviceBox.ItemsSource = collection;
        var driveinfo = DriveInfo.GetDrives();

        foreach (var mount in mtab)
        {
            if (mount.StartsWith("/dev/sd") || mount.StartsWith("/dev/nvme"))
            {
                var dev = mount.Split(" ");
                if (!BlockList.Contains(dev[1]))
                {
                    var devn = dev[0].Split("/").Last();
                    for (int x = devn.Length - 1; x > 0; x--)
                    {
                        if (!char.IsDigit(devn[x]))
                        {
                            if (devn.StartsWith("nvme") && devn[x] == 'p')
                            {
                                devn = devn[..(x)];
                            }
                            else
                            {
                                devn = devn[..(x + 1)];
                            }
                            break;

                        }
                    }
                    var a = File.ReadAllText($"/sys/block/{devn}/removable");
                    
                    Device device = new()
                    {
                        Model = File.ReadAllText($"/sys/block/{devn}/device/model"),
                        MountPoint = dev[1],
                        Type = DriveType.Unknown,
                        Format= dev[2]
                    };

                    var d = driveinfo.FirstOrDefault(x => x.Name == dev[1]);
                    if (d != null)
                    {
                        device.Usage = d.TotalSize - d.TotalFreeSpace;
                        device.Size = d.TotalSize;
                        device.Type = d.DriveType;
                    }
                    collection.Add(device);

                }
            }
        }
    }
    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    public string InvalidPathCharactersForFormat(string format)
    {
        return format.ToLower() switch
        {
            "exfat" or "fat" or "vfat" => "*?.,;:/\\|+=<>[]\"\0",
            "ntfs" or "fuseblk" => "/\0",
            "btrfs" or "ext4" or "ext3" =>"\0"
        };
    }
    public string InvalidFileCharactersForFormat(string format)
    {
        return format.ToLower() switch
        {
            "exfat" or "fat" or "vfat" => "*?,;:/\\|+=<>[]\"\0",
            "ntfs" or "fuseblk" => "/\0",
            "btrfs" or "ext4" or "ext3" =>"\0"
        };
    }
    public string Clean(string i, string notallowed)
    {
        foreach (var invalid in notallowed) 
        {
            i = i.Replace(invalid, '_');
        }

        return i;
    }
   
    public async Task SyncAsync(Device dev)
    {
        Lock = true;
        
        if (dev == null)
        {
            return;
        }
        List<Song> list = (await env.GetQueue())!;
        void scroll()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ScrollToBottom.IsChecked == true)
                {
                    TextBox.ScrollToLine(TextBox.LineCount);
                }
            }, DispatcherPriority.Background);
        }

        void Append(string Text)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ScrollToBottom.IsChecked == true)
                {
                    TextBox.AppendText(Text + "\n");
                }
            }, DispatcherPriority.Background);
        }
        double FullSize = list.Where(x => x?.Metadata?.Duration is not null).Select(x => x.Metadata.Duration).Sum() ?? 1;
        double CurrSize = 0;
        for (int i = 0; i < list.Count; i++)
        {
            Song? song = list[i];
            if (song.Stream is WrappedFileStream wfs)
            {
                Append(wfs.URL);
                scroll();
                var path = Path.Combine(dev.MountPoint, "Music", Clean(song.Metadata.Artist, InvalidPathCharactersForFormat(dev.Format)), Clean(song.Metadata.Album,InvalidPathCharactersForFormat(dev.Format))
                  );
                Directory.CreateDirectory(path);
                var loc = Path.Combine(path, Clean(song.Metadata.DiscNumber + "." + song.Metadata.TrackNumber + " " +
                                                    song.Metadata.Title +
                                                    ".mp3", InvalidFileCharactersForFormat(dev.Format)));
                if (File.Exists(loc))
                {
                    continue;
                }
                Append(loc);
                scroll();
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo("ffmpeg", $"-i \"{wfs.URL}\" -b:a 320k \"{loc}\"")
                    {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    }
                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.OutputDataReceived += (s, e) => Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Append(e.Data);
                    scroll();
                });
                proc.ErrorDataReceived += (s, e) => Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Append(e.Data);
                    scroll();
                });
                Append($"{i + 1}/{list.Count} {(double)(i + 1) / list.Count * 100}N% {(CurrSize / FullSize) * 100}L%\n");
                scroll();

                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0)
                {
                    Append("FAIL\n");
                    scroll();
                }
                CurrSize += (double)song?.Metadata?.Duration;
            }
        }
        Append("Done with queue\n");
        Lock = false;


    }

    private void Sync_OnClick(object? sender, RoutedEventArgs e)
    {
        var dev = (Device?)DeviceBox.SelectedItem;
        Task.Run(async () => await SyncAsync(dev));
    }
}

[Export(typeof(ISyncPlugin))]

public class LSync : ISyncPlugin
{
    public void Use(ISyncEnvironmentListener env)
    {
        MainWindow mw = new(env);
        mw.Show();
    }

    public string Name => "Sync plugin";
    public string Description => "Now with 100% more windows";
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(LSync).Assembly,
        "SilverAudioPlayer.Any.Sync.Sync.svg");

    public Version? Version => typeof(LSync).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Linux.Sync\nGPL3";
    public List<Tuple<Uri, URLType>>? Links => null;
}
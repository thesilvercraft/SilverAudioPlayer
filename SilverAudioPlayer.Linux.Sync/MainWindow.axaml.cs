using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SilverAudioPlayer.Shared;
using Splat.ModeDetection;
using Humanizer;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Linux.Sync;

public class Device
{
    public string Model;
    public string MountPoint;
    public bool Removable;
    public long Size=0;
    public long Usage=0;
    public override string ToString()
    {
        return $"{Model} {MountPoint} {(Removable?"Removable":"Non removable")} {Usage.Bytes()}/{Size.Bytes()}";
    } 
}
public partial class MainWindow : Window
{
    public MainWindow()
    {
        if (!RuntimeInformation
            .IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }
        InitializeComponent();
        DeviceBox = this.FindControl<ComboBox>("DeviceBox");
        TextBox = this.FindControl<TextBox>("TextBox");
        RefreshDevices();
        if(WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks")==true)
        {
            return;
        }
        this.DoAfterInitTasks(true);
    }

    private ISyncEnvironmentListener env;

    public MainWindow(ISyncEnvironmentListener env) : this()
    {
        this.env = env;
    }

    public string[] BlockList = new[] { "/boot/efi", "/boot" };
    public void RefreshDevices()
    {
        var mtab = File.ReadAllLines("/etc/mtab");
        var collection = new ObservableCollection<Device>();
        DeviceBox.Items = collection;
        var driveinfo = DriveInfo.GetDrives();
            
        foreach (var mount in mtab)
        {
            if (mount.StartsWith("/dev/sd") || mount.StartsWith("/dev/nvme"))
            {
                var dev = mount.Split(" ");
                if (!BlockList.Contains( dev[1]) )
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
                                devn = devn[..(x+1)];
                            }
                            break;

                        }
                    }
                    var a = File.ReadAllText($"/sys/block/{devn}/removable");
                    Device device = new()
                    {
                        Model =  File.ReadAllText($"/sys/block/{devn}/device/model"),
                        MountPoint = dev[1],
                        Removable = (File.ReadAllText($"/sys/block/{devn}/removable")=="1\n")
                    };
                    
                    var d = driveinfo.FirstOrDefault(x => x.Name == dev[1]);
                    if (d != null)
                    {
                        device.Usage = d.TotalSize-d.TotalFreeSpace;
                        device.Size = d.TotalSize;
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

    public string Clean(string i)
    {
        foreach (var invalid in  Path.GetInvalidPathChars())
        {
            i = i.Replace(invalid, '_');
        }

        return i;
    }
    public string CleanF(string i)
    {
        foreach (var invalid in  Path.GetInvalidFileNameChars())
        {
            i = i.Replace(invalid, '_');
        }

        return i;
    }
    public async Task SyncAsync()
    {
        var dev = (Device?)DeviceBox.SelectedItem;
        if (dev == null)
        {
            return;
        }
        TextBox.CaretIndex = int.MaxValue;
        foreach (var song in (await env.GetQueue())!)
        {
            if (song.Stream is WrappedFileStream wfs)
            {
                TextBox.Text += wfs.URL+"\n";
                var path = Path.Combine(dev.MountPoint, "Music", Clean(song.Metadata.Artist), Clean(song.Metadata.Album)
                  );
                Directory.CreateDirectory(path);
                var loc = Path.Combine(path, CleanF(song.Metadata.DiscNumber + "." + song.Metadata.TrackNumber + " " +
                                                    song.Metadata.Title +
                                                    ".mp3"));
                TextBox.Text += loc+"\n";
                
                var proc = new Process(  );
                proc.StartInfo = new ProcessStartInfo("ffmpeg", $"-i \"{wfs.URL}\" \"{loc}\"")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,

                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.OutputDataReceived += (s, e) =>    Dispatcher.UIThread.InvokeAsync(() => TextBox.Text +=e.Data+"\n"); ;
                proc.ErrorDataReceived += (s, e) => Dispatcher.UIThread.InvokeAsync(() => TextBox.Text +=e.Data +"\n");
                
                await proc.WaitForExitAsync();
            }
        }
        TextBox.Text +="Done with queue";
    }
    private  void Sync_OnClick(object? sender, RoutedEventArgs e)
    {
        //TODO SYNC
        Dispatcher.UIThread.Post(() => SyncAsync(), DispatcherPriority.Background);
        
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

    public string Name => "Linux sync plugin";
    public string Description => "made using lots of cats";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(LSync).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Linux.Sync\nGPL3";
    public List<Tuple<Uri, URLType>>? Links => null;
}
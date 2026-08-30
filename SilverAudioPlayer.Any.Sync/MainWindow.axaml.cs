using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.Sync;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DeviceBox = this.FindControl<ComboBox>("DeviceBox");
        TextBox = this.FindControl<TextEditor>("TextBox");
        RefreshDevices();
        Closing += (e, o) => { o.Cancel = Lock; };
        if (WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks") == true)
        {
            return;
        }
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
        }
        this.DoAfterInitTasks(true);
    }
    internal SyncConfig config;
    public bool Lock = false;

    private ISyncEnvironmentListener env;

    public MainWindow(ISyncEnvironmentListener env) : this()
    {
        this.env = env;
    }

    public void RefreshDevices()
    {
        if (OperatingSystem.IsWindows())
        {
            FileSystemHelper.RefreshDevicesWindows((x) =>
            {
                DeviceBox.ItemsSource = x;
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            FileSystemHelper.RefreshDevicesLinux((x) =>
            {
                DeviceBox.ItemsSource = x;
            });
        }
    }


    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    public async Task SyncAsync(string baseLoc, string format)
    {
        if (Lock) return;
        if ( string.IsNullOrWhiteSpace(baseLoc))            return;
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
                TextBox.AppendText(Text + "\n");
            }, DispatcherPriority.Background);
        }
        if (!Path.Exists(baseLoc))
        {
            if (Directory.GetParent(baseLoc) is { Exists: true })
            {
                Directory.CreateDirectory(baseLoc);
            }
            else
            {
                Append($"Path not found {baseLoc}");
                scroll();
                return;
            }
        }
        Lock = true;
        var list = (await env.GetQueue())!;
      
        try
        {

            double FullSize = list.Where(x => x?.Metadata?.Duration is not null).Select(x => x.Metadata.Duration).Sum() ?? 1;
            double CurrSize = 0;
            Dispatcher.UIThread.Post(() =>
            {
                ProgressBar.Value = CurrSize;
                ProgressBar.Maximum = FullSize;
            }, DispatcherPriority.Background);
            for (int i = 0; i < list.Count; i++)
            {
                Song? song = list[i];
                string songLocation;
                bool TempLoc = false;
                if (song.Stream is WrappedFileStream wfs)
                {
                    songLocation = wfs.URL;
                }
                else
                {
                    TempLoc = true;
                    var fnamm = Path.Combine( Path.GetTempPath(), song.Guid.ToString() + song.Stream.MimeType.FileExtensions[0]);
                    using (FileStream fs = new FileStream(fnamm, FileMode.Create))
                    {
                        using var reader = song.Stream.GetStream();
                        await reader.CopyToAsync(fs);
                    }
                     songLocation = fnamm;
                }
                Append(songLocation);
                scroll();
                var songFormat = new Dictionary<string, string>{
                    {"Album", FileSystemHelper.Clean(song.Metadata.Album??"Unknown album", FileSystemHelper.InvalidPathCharactersForFormat(format)) },
                    {"Artist", FileSystemHelper.Clean(song.Metadata.Artist??"Unknown artist", FileSystemHelper.InvalidPathCharactersForFormat(format))},
                    {"DiscNumber",song.Metadata.DiscNumber?.ToString()??"?"},
                    {"TrackNumber",song.Metadata.TrackNumber?.ToString()??"?"},
                    {"Title",song.Metadata.Title},
                    {"Extension",config.PreferredFileExtension},
                    {"Year",song.Metadata.Year?.ToString()??"?"},
                    {"Genre", song.Metadata.Genre},
                    {"Bitrate", song.Metadata.Bitrate?.ToString()??"?"},
                };
                var path = Path.Combine(baseLoc, Formatter.Format(config.PathToStore, songFormat));
                Directory.CreateDirectory(path);
                var loc = Path.Combine(path, FileSystemHelper.Clean(Formatter.Format(config.NameFormatting, songFormat), FileSystemHelper.InvalidFileCharactersForFormat(format)));
                if (File.Exists(loc))
                {
                    continue;
                }
                var arguments = Formatter.Format(config.FFmpegOptions, new Dictionary<string, string>(){
                    {"in", songLocation},
                    {"out", loc}
                });
                Append($"Calling ffmpeg {arguments}");
                scroll();
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo("ffmpeg", arguments)
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
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressBar.Value = CurrSize;
                }, DispatcherPriority.Background);
                scroll();

                await proc.WaitForExitAsync();
                if (song?.Metadata?.SyncedLyrics?.Count is not (0 or null) && !TempLoc)
                {
                    var possiblelrc = Path.ChangeExtension(songLocation, "lrc");
                    if (Path.Exists(possiblelrc))
                    {
                        File.Copy(possiblelrc, Path.ChangeExtension(loc, "lrc") );
                    }
                }
                    
                if (proc.ExitCode != 0)
                {
                    Append("FAIL\n");
                    scroll();
                }

                if (TempLoc)
                {
                    File.Delete(loc);
                }
                CurrSize += (double)song?.Metadata?.Duration;
            }

            Append("Done with queue\n");
        }
        catch(Exception e)
        {
            Append(e.ToString());
            throw;
        }
        Lock = false;
    }

    private void Sync_OnClick(object? sender, RoutedEventArgs e)
    {
        var dev = ((Device?)DeviceBox.SelectedItem)?.Format??"";
        var loc = LocationBox.Text;
        Task.Run(async () => await SyncAsync(loc, dev));
    }

    private void LocationBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DeviceBox.SelectedItem is Device dev)
        {
            var t = LocationBox.Text;
            if (t == null  || !t.StartsWith(dev.MountPoint))
            {
                DeviceBox.SelectedItem = null;
            }
        }
    }

    private void DeviceBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DeviceBox.SelectedItem is Device dev)
        {
            var t = LocationBox.Text;
            if (t == null || !t.StartsWith(dev.MountPoint))
            {
                LocationBox.Text= Path.Combine(dev.MountPoint,"Music");
            }
        }
    }
}
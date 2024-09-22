using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverConfig;
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

    public async Task SyncAsync(Device? dev)
    {
        Lock = true;
        if (dev == null)            return;
        var list = (await env.GetQueue())!;
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
                if (song.Stream is not WrappedFileStream wfs) continue;
                Append(wfs.URL);
                scroll();
                var songFormat = new Dictionary<string, string>{
                    {"Album", FileSystemHelper.Clean(song.Metadata.Album??"Unknown album", FileSystemHelper.InvalidPathCharactersForFormat(dev.Format)) },
                    {"Artist", FileSystemHelper.Clean(song.Metadata.Artist??"Unknown artist", FileSystemHelper.InvalidPathCharactersForFormat(dev.Format))},
                    {"DiscNumber",song.Metadata.DiscNumber?.ToString()??"?"},
                    {"TrackNumber",song.Metadata.TrackNumber?.ToString()??"?"},
                    {"Title",song.Metadata.Title},
                    {"Extension",config.PreferredFileExtension},
                    {"Year",song.Metadata.Year?.ToString()??"?"},
                    {"Genre", song.Metadata.Genre},
                    {"Bitrate", song.Metadata.Bitrate?.ToString()??"?"},
                };
                var path = Path.Combine(dev.MountPoint, Formatter.Format(config.PathToStore, songFormat));
                Directory.CreateDirectory(path);
                var loc = Path.Combine(path, FileSystemHelper.Clean(Formatter.Format(config.NameFormatting, songFormat), FileSystemHelper.InvalidFileCharactersForFormat(dev.Format)));
                if (File.Exists(loc))
                {
                    continue;
                }
                var arguments = Formatter.Format(config.FFmpegOptions, new Dictionary<string, string>(){
                    {"in", wfs.URL},
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
                if (song?.Metadata?.SyncedLyrics?.Count is not (0 or null))
                {
                    var possiblelrc = Path.ChangeExtension(wfs.URL, "lrc");
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
        var dev = (Device?)DeviceBox.SelectedItem;
        Task.Run(async () => await SyncAsync(dev));
    }
}
public static class Formatter
{
    public static string Format(string input, Dictionary<string, string> arguments)
    {
        var stringBuilder = new StringBuilder(input);
        foreach (var argument in arguments)
        {
            stringBuilder.Replace($"{{{argument.Key}}}", argument.Value);
        }
        return stringBuilder.ToString();

    }
}
public class SyncConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("used verbatim when calling FFmpeg")]

    public string PreferredFileExtension { get; set; } = ".mp3";
    [Comment("used verbatim when calling FFmpeg")]
    public string FFmpegOptions { get; set; } = "-i \"{in}\" -b:a 320k \"{out}\"";

    [Comment("used verbatim when calling FFmpeg")]
    public string PathToStore { get; set; } = $"Music{Path.DirectorySeparatorChar}{{Album}}";
    [Comment("used verbatim when calling FFmpeg")]
    public string NameFormatting { get; set; } = "{DiscNumber}.{TrackNumber}.{Title}{Extension}";
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}
[Export(typeof(ISyncPlugin))]

public class LSync : ISyncPlugin, IAmOnceAgainAskingYouForYourMemory
{
    public ObjectToRemember ConfigObject =
        new(Guid.Parse("720fe248-4d44-4427-8d72-578ba565f33c"), new SyncConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject]; public void Use(ISyncEnvironmentListener env)
    {
        MainWindow mw = new(env)
        {
            config = (SyncConfig)ConfigObject.Value
        };
        mw.Show();
    }

    public string Name => "Sync plugin";
    public string Description => "Move your queue over to a sd card, usb flash drive or store it in ram (/tmp) as mp3s using ffmpeg";
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(LSync).Assembly,
        "SilverAudioPlayer.Any.Sync.Sync.svg");

    public Version? Version => typeof(LSync).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Linux.Sync\nGPL3";
    public List<Tuple<Uri, URLType>>? Links => null;
}
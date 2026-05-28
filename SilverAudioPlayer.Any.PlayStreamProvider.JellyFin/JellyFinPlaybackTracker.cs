using System.Composition;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;
[Export(typeof(IMusicStatusInterface))]
public partial class JellyFinPlaybackTracker:IMusicStatusInterface
{
    public void Dispose()
    {
    }

    public string Name =>"JellyFin playback tracker";
    public string Description => "Tracks playback by sending playback events to server";
    public WrappedStream? Icon => null;
    public Version? Version=>typeof(JellyFinPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "";
    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri(
                "https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.PlayStreamProvider.JellyFin"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/Jellyfin.Sdk/{typeof(JellyfinApiClient).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/jellyfin/jellyfin-sdk-csharp"), URLType.LibraryCode)
    ];

    private IMusicStatusInterfaceListener listener;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        if(_started) return;
        var helper = JellyFinPlayStreamProvider.GetJellyFinHelper();
        if (!helper.GetValidUser()) return;
        this.helper = helper;
        this.listener = listener;
        this.listener.TrackChangedNotification += SongChanged;
        this.listener.PositionChanged += Seeked;
        this.listener.PlayerStateChanged += StateChanged;
        _started = true;
    }

    private void StateChanged(object? sender, PlaybackState e)
    {
        var currentSong = listener.GetCurrentTrack();
        if (currentSong?.Stream is not WrappedHttpStream httpStream ||
            JfIdregx.Match(httpStream.Url) is not { Success: true } match) return;
        Debug.WriteLine($"We seeked {match.Groups["id"]}");
        switch (e)
        {
            case PlaybackState.Stopped:
                Task.Run(async () =>
                {
                    await helper.apiClient.Sessions.Playing.Stopped.PostAsync(new PlaybackStopInfo()
                    {
                        ItemId = new Guid(match.Groups["id"].Value),
                        PositionTicks = (long?)(listener.GetPosition()*10000000),
                    });
                });
                break;
            case PlaybackState.Playing:
                Task.Run(async () =>
                {
                    await helper.apiClient.Sessions.Playing.Progress.PostAsync(new PlaybackProgressInfo()
                    {
                        ItemId = new Guid(match.Groups["id"].Value),
                        IsPaused = false,
                        PositionTicks = (long?)(listener.GetPosition()*10000000),
                    });
                });
                break;
            case PlaybackState.Paused:
                Task.Run(async () =>
                {
                    await helper.apiClient.Sessions.Playing.Progress.PostAsync(new PlaybackProgressInfo()
                    {
                        ItemId = new Guid(match.Groups["id"].Value),
                        IsPaused = true,
                        PositionTicks = (long?)(listener.GetPosition()*10000000),
                    });
                });
                break;
            case PlaybackState.Buffering:
                Task.Run(async () =>
                {
                    await helper.apiClient.Sessions.Playing.Progress.PostAsync(new PlaybackProgressInfo()
                    {
                        ItemId = new Guid(match.Groups["id"].Value),
                        IsPaused = true,
                        PositionTicks = (long?)(listener.GetPosition()*10000000),
                    });
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e), e, null);
        }
    }

    private void Seeked(object? sender, TimeSpan e)
    {
        var currentSong = listener.GetCurrentTrack();
        if (currentSong.Stream is not WrappedHttpStream httpStream ||
            JfIdregx.Match(httpStream.Url) is not { Success: true } match) return;
        Debug.WriteLine($"We seeked {match.Groups["id"]}");
        Task.Run(async () =>
        {
            await helper.apiClient.Sessions.Playing.Progress.PostAsync(new PlaybackProgressInfo()
            {
                ItemId = new Guid(match.Groups["id"].Value),
                IsPaused = false,
                PositionTicks = (long?)(e.TotalSeconds*10000000)
            });
        });
    }

    private JellyFinHelper helper;

    private Regex JfIdregx = MyRegex();
    
    private void SongChanged(object? sender, Song e)
    {
        /*if (e.Metadata is MetadataCombo combo && combo.OriginalMetadata.FirstOrDefault(x=>x is JellyfinMeta) is JellyfinMeta meta)
        {
            //jellyfin is def our source
        }*/
        if (e.Stream is not WrappedHttpStream httpStream ||
            JfIdregx.Match(httpStream.Url) is not { Success: true } match) return;
        Debug.WriteLine($"We started playing {match.Groups["id"]}");
        Task.Run(async () =>
        {
            await helper.apiClient.Sessions.Playing.PostAsync(new PlaybackStartInfo()
            {
                ItemId = new Guid(match.Groups["id"].Value),
                IsPaused = false,
                PositionTicks = (long?)(listener.GetPosition()*10000000)
            });
        });
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        if(!_started) return;
        this.listener.TrackChangedNotification -= SongChanged;
        this.listener.PositionChanged -= Seeked;
        this.listener.PlayerStateChanged -= StateChanged;

        this.listener = null;
        this.helper = null;
        _started=false;
    }
    bool _started=false;
    public bool IsStarted => _started;

    [GeneratedRegex("(.*)\\/Audio\\/(?<id>.*?)\\/universal", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
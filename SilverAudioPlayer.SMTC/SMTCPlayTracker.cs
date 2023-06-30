using SilverAudioPlayer.Shared;
using System.Composition;
using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SilverAudioPlayer.SMTC;

[Export(typeof(IMusicStatusInterface))]
[SupportedOSPlatform("windows10.0.17763.24")]
public class SMTCPlayTracker : IMusicStatusInterface
{
    private MediaPlayer? _mediaPlayer;
    private SystemMediaTransportControls? _systemMediaTransportControls;
    private readonly bool DISABLE = false;
    private bool disposedValue;
    private List<string> TempFiles = new();
    private System.Timers.Timer TimeLineTimer;

    public SMTCPlayTracker()
    {
        if (Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < 17763)
        {
            Debug.WriteLine("!!!!!!!!!! SMTC DISABLED !!!!!!!!!!");
            DISABLE = true;
        }
    }

    public Version? Version => typeof(SMTCPlayTracker).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.SMTC"),
            URLType.Code),
        new(new("https://docs.microsoft.com/en-us/uwp/api/windows.media.systemmediatransportcontrols?view=winrt-22621"),
            URLType.LibraryDocumentation)
    };

    public void PlayerStateChanged(object obj, PlaybackState newstate)
    {
        if (DISABLE) return;

        if (_systemMediaTransportControls != null)
            switch (newstate)
            {
                case PlaybackState.Stopped:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;

                case PlaybackState.Playing:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;

                case PlaybackState.Paused:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;

                case PlaybackState.Buffering:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    break;
            }
    }




    public string Name => "SystemMediaTransportControls" + (DISABLE ? " (DISABLED)" : "");

    public string Description => "Windows 10 / ModernFlyouts integration, interfaces through SMTC";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SMTCPlayTracker).Assembly,
        "SilverAudioPlayer.Windows10.MusicStatusInterface.SMTC.SMTC.svg");

    public string Licenses => "SilverAudioPlayer.SMTC\nThis program is free software: you can redistribute it and/or modify\r\n    it under the terms of the GNU General Public License as published by\r\n    the Free Software Foundation, either version 3 of the License, or\r\n    (at your option) any later version.\r\n\r\n    This program is distributed in the hope that it will be useful,\r\n    but WITHOUT ANY WARRANTY; without even the implied warranty of\r\n    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the\r\n    GNU General Public License for more details.\r\n\r\n    You should have received a copy of the GNU General Public License\r\n    along with this program.  If not, see <https://www.gnu.org/licenses/>";

    public async void TrackChangedNotification(object obj, Song newtrack)
    {
        if (DISABLE) return;
        if (_systemMediaTransportControls != null && newtrack != null)
        {
            _systemMediaTransportControls.IsEnabled = true;
            SystemMediaTransportControlsDisplayUpdater updater = _systemMediaTransportControls.DisplayUpdater;
            updater.ClearAll();
            updater.Type = MediaPlaybackType.Music;
            updater.AppMediaId = newtrack.Guid.ToString();
            updater.MusicProperties.Artist = newtrack?.Metadata?.Artist;
            updater.MusicProperties.AlbumTitle = newtrack?.Metadata?.Album;
            updater.MusicProperties.Title = newtrack?.Metadata?.Title ?? newtrack?.Name;
            updater.MusicProperties.TrackNumber = (uint)(newtrack?.Metadata?.TrackNumber ?? 0);
            //string s = newtrack.URI.ToLower();
            // bool isWeb = s.StartsWith("http://") || s.StartsWith("https://");

            if (newtrack?.Metadata?.Pictures?.Count >= 1)
            {
                var first = newtrack?.Metadata?.Pictures?[0];
                if (first != null)
                {
                    var fullPath = Path.GetTempFileName();
                    await using var imageData = first.Data.GetStream();
                    FileStream fs = new(fullPath, FileMode.OpenOrCreate);
                    await imageData.CopyToAsync(fs);
                    fs.Flush();
                    fs.Close();
                    await fs.DisposeAsync();
                    TempFiles.Add(fullPath);
                    updater.Thumbnail =
                        RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(fullPath));
                }
            }

            updater.Update();
        }
        else if (_systemMediaTransportControls != null)
        {
            _systemMediaTransportControls.IsEnabled = false;
        }
    }


    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    private void UpdateTimeline(PlaybackState state, Song track, double pos)
    {
        if (DISABLE) return;
        var timelineProperties = new SystemMediaTransportControlsTimelineProperties();
        if (track != null)
        {
            var dur = Env.GetDuration();
            timelineProperties.StartTime = TimeSpan.FromSeconds(0);
            timelineProperties.MinSeekTime = TimeSpan.FromSeconds(0);
            timelineProperties.EndTime = TimeSpan.FromSeconds(dur);
            timelineProperties.Position = TimeSpan.FromSeconds(pos);
            timelineProperties.MaxSeekTime = TimeSpan.FromSeconds(dur);
        }

        _systemMediaTransportControls?.UpdateTimelineProperties(timelineProperties);
    }

    private void SystemControls_PlaybackPositionChangeRequested(SystemMediaTransportControls sender,
        PlaybackPositionChangeRequestedEventArgs args)
    {
        if (DISABLE) return;
        Env.SetPosition((ulong)args.RequestedPlaybackPosition.TotalSeconds);

    }

    private void SystemControls_AutoRepeatModeChangeRequested(SystemMediaTransportControls sender,
        AutoRepeatModeChangeRequestedEventArgs args)
    {
        if (DISABLE) return;
        var a = args.RequestedAutoRepeatMode switch
        {
            MediaPlaybackAutoRepeatMode.None => RepeatState.None,
            MediaPlaybackAutoRepeatMode.Track => RepeatState.One,
            MediaPlaybackAutoRepeatMode.List => RepeatState.Queue,
            _ => RepeatState.None
        };
        sender.AutoRepeatMode = args.RequestedAutoRepeatMode;
        Env.SetRepeat(a);
    }

    private void SystemControls_ShuffleEnabledChangeRequested(SystemMediaTransportControls sender,
        ShuffleEnabledChangeRequestedEventArgs args)
    {
        if (DISABLE) return;
        Env.SetShuffle(args.RequestedShuffleEnabled);

    }

    private void PlayOrResume()
    {
        switch (Env.GetState())
        {
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                Env.Play();

                break;
        }
    }

    private void PauseOrResume()
    {
        switch (Env.GetState())
        {
            case PlaybackState.Playing:
                Env.Pause();
                break;
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                Env.Play();
                break;
        }
    }

    private void SystemControls_ButtonPressed(SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        if (DISABLE) return;
        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play:
                PlayOrResume();
                break;

            case SystemMediaTransportControlsButton.Pause:
                PauseOrResume();
                break;

            case SystemMediaTransportControlsButton.Stop:
                Env.Stop();
                break;

            case SystemMediaTransportControlsButton.Next:
                Env.Next();
                break;

            case SystemMediaTransportControlsButton.Previous:
                Env.Previous();
                break;
        }
    }

    internal static IRandomAccessStream ConvertTo(byte[] arr)
    {
        MemoryStream stream = new(arr)
        {
            Position = 0
        };
        return stream.AsRandomAccessStream();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing) _mediaPlayer?.Dispose();
            _systemMediaTransportControls = null;

            disposedValue = true;
        }
    }
    IMusicStatusInterfaceListener Env;

    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        Env = listener;
        Env.TrackChangedNotification += TrackChangedNotification;
        Env.PlayerStateChanged += PlayerStateChanged;
        if (DISABLE) return;
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.CommandManager.IsEnabled = true;
        _systemMediaTransportControls = _mediaPlayer.SystemMediaTransportControls;
        _systemMediaTransportControls.IsEnabled = true;
        _systemMediaTransportControls.IsNextEnabled = true;
        _systemMediaTransportControls.IsPauseEnabled = true;
        _systemMediaTransportControls.IsPlayEnabled = true;
        _systemMediaTransportControls.IsPreviousEnabled = true;
        _systemMediaTransportControls.IsStopEnabled = true;
        _systemMediaTransportControls.IsRewindEnabled = false;
        _systemMediaTransportControls.IsFastForwardEnabled = false;
        _systemMediaTransportControls.ShuffleEnabled = true;
        _mediaPlayer.IsLoopingEnabled = false;

        _systemMediaTransportControls.ButtonPressed += SystemControls_ButtonPressed;
        _systemMediaTransportControls.ShuffleEnabledChangeRequested += SystemControls_ShuffleEnabledChangeRequested;
        _systemMediaTransportControls.AutoRepeatModeChangeRequested += SystemControls_AutoRepeatModeChangeRequested;
        _systemMediaTransportControls.PlaybackPositionChangeRequested += SystemControls_PlaybackPositionChangeRequested;
        TimeLineTimer = new(interval: 1000);
        TimeLineTimer.Elapsed += (s, e) =>
        {
            var state = Env.GetState();
            var track = Env.GetCurrentTrack();
            var pos = Env.GetPosition();
            UpdateTimeline(state, track, pos);
        };
        TimeLineTimer.Start();
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        if (DISABLE) return;
        TimeLineTimer.Stop();
        Env.TrackChangedNotification -= TrackChangedNotification;
        Env.PlayerStateChanged -= PlayerStateChanged;

        _mediaPlayer?.Dispose();
        _systemMediaTransportControls = null;
        TimeLineTimer.Dispose();
        foreach (var file in TempFiles)
            if (File.Exists(file))
                File.Delete(file);
    }

}
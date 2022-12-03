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

    public void PlayerStateChanged(PlaybackState newstate)
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
                    TimeLineTimer.Start();
                    break;

                case PlaybackState.Paused:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;

                case PlaybackState.Buffering:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    TimeLineTimer.Start();
                    break;
            }
    }

    public void StartIPC()
    {
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
            if (GetState != null && GetCurrentTrack != null && GetPosition != null)
            {
                var state = GetState();
                var track = GetCurrentTrack();
                var pos = GetPosition();
                UpdateTimeline(state, track, pos);
                if (state != PlaybackState.Playing && state != PlaybackState.Buffering) TimeLineTimer.Stop();
            }
            else
            {
                TimeLineTimer.Stop();
            }
        };
        TimeLineTimer.Start();
    }

    public void StopIPC()
    {
        if (DISABLE) return;
        _mediaPlayer?.Dispose();
        _systemMediaTransportControls = null;
        TimeLineTimer.Dispose();
        foreach (var file in TempFiles)
            if (File.Exists(file))
                File.Delete(file);
    }

    public string Name => "SystemMediaTransportControls" + (DISABLE ? " (DISABLED)" : "");

    public string Description => "Windows 10 / ModernFlyouts integration, interfaces through SMTC";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SMTCPlayTracker).Assembly,
        "SilverAudioPlayer.Windows10.MusicStatusInterface.SMTC.SMTC.svg");

    public string Licenses => "SilverAudioPlayer.SMTC\nThis program is free software: you can redistribute it and/or modify\r\n    it under the terms of the GNU General Public License as published by\r\n    the Free Software Foundation, either version 3 of the License, or\r\n    (at your option) any later version.\r\n\r\n    This program is distributed in the hope that it will be useful,\r\n    but WITHOUT ANY WARRANTY; without even the implied warranty of\r\n    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the\r\n    GNU General Public License for more details.\r\n\r\n    You should have received a copy of the GNU General Public License\r\n    along with this program.  If not, see <https://www.gnu.org/licenses/>";

    public async void TrackChangedNotification(Song newtrack)
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
                    FileStream fs = new(fullPath, FileMode.OpenOrCreate);
                    fs.Write(first.Data);
                    fs.Flush();
                    fs.Close();
                    fs.Dispose();
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

    public event EventHandler Play;

    public event EventHandler Pause;

    public event EventHandler PlayPause;

    public event EventHandler Stop;

    public event EventHandler Next;

    public event EventHandler Previous;

    public event EventHandler<byte> SetVolume;

    public event Func<byte> GetVolume;

    public event Func<Song> GetCurrentTrack;

    public event Func<ulong> GetDuration;

    public event EventHandler<ulong> SetPosition;

    public event Func<ulong> GetPosition;

    public event Func<PlaybackState> GetState;

    public event EventHandler<IMusicStatusInterface> StateChangedNotification;

    public event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

    public event Func<RepeatState> GetRepeat;

    public event EventHandler<RepeatState> SetRepeat;

    public event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

    public event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

    public event Func<bool> GetShuffle;

    public event EventHandler<bool> SetShuffle;

    public event EventHandler<IMusicStatusInterface> RatingChangedNotification;

    public event EventHandler<byte> SetRating;

    public event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

    public event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

    public event EventHandler<IMusicStatusInterface> NewLyricsNotification;

    public event EventHandler<IMusicStatusInterface> NewCoverNotification;

    public event Func<string> GetLyrics;

    private void UpdateTimeline(PlaybackState state, Song track, ulong pos)
    {
        if (DISABLE) return;
        var timelineProperties = new SystemMediaTransportControlsTimelineProperties();
        if (track != null && GetDuration != null)
        {
            var dur = GetDuration();
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
        SetPosition?.Invoke(this, (ulong)args.RequestedPlaybackPosition.TotalSeconds);
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
        SetRepeat?.Invoke(this, a);
    }

    private void SystemControls_ShuffleEnabledChangeRequested(SystemMediaTransportControls sender,
        ShuffleEnabledChangeRequestedEventArgs args)
    {
        if (DISABLE) return;
        SetShuffle?.Invoke(this, args.RequestedShuffleEnabled);
    }

    private void PlayOrResume()
    {
        switch (GetState())
        {
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                Play?.Invoke(this, EventArgs.Empty);
                break;
        }

        ;
    }

    private void PauseOrResume()
    {
        switch (GetState())
        {
            case PlaybackState.Playing:

                Pause?.Invoke(this, EventArgs.Empty);
                break;
            case PlaybackState.Stopped:
            case PlaybackState.Paused:

                Play?.Invoke(this, EventArgs.Empty);
                break;
        }

        ;
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
                Stop?.Invoke(this, EventArgs.Empty);
                break;

            case SystemMediaTransportControlsButton.Next:
                Next?.Invoke(this, EventArgs.Empty);
                break;

            case SystemMediaTransportControlsButton.Previous:
                Previous?.Invoke(this, EventArgs.Empty);
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
}
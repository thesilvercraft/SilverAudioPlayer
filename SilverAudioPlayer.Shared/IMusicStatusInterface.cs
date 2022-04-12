using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Shared
{
    public interface IMusicStatusInterface : IDisposable
    {
        void StartIPC();

        void StopIPC();

        event EventHandler Play;

        event EventHandler Pause;

        event EventHandler PlayPause;

        event EventHandler Stop;

        event EventHandler Next;

        event EventHandler Previous;

        event EventHandler<byte> SetVolume;

        event Func<byte> GetVolume;

        event EventHandler<byte> VolumeChangedNotification;

        event Func<Song> GetCurrentTrack;

        event Func<ulong> GetDuration;

        event EventHandler<ulong> SetPosition;

        event Func<ulong> GetPosition;

        event EventHandler<IMusicStatusInterface> TrackChangedNotification;

        event EventHandler<IMusicStatusInterface> ShowWindow;

        event Func<PlaybackState> GetState;

        event EventHandler<IMusicStatusInterface> StateChangedNotification;

        event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

        event Func<RepeatState> GetRepeat;

        event EventHandler<RepeatState> SetRepeat;

        event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

        event EventHandler<IMusicStatusInterface> Close;

        event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

        event Func<bool> GetShuffle;

        event EventHandler<bool> SetShuffle;

        event EventHandler<IMusicStatusInterface> RatingChangedNotification;

        event EventHandler<byte> SetRating;

        event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

        event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

        event EventHandler<IMusicStatusInterface> NewLyricsNotification;

        event EventHandler<IMusicStatusInterface> NewCoverNotification;

        event Func<string> GetLyrics;
    }
}
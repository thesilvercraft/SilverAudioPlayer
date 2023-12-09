using System.Runtime.CompilerServices;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace SilverAudioPlayer.Linux.MPRIS
{
    [DBusInterface("org.mpris.MediaPlayer2.Player")]
    interface IPlayer : IDBusObject
    {
        Task NextAsync();
        Task PreviousAsync();
        Task PauseAsync();
        Task PlayPauseAsync();
        Task StopAsync();
        Task PlayAsync();
        Task SeekAsync(long Offset);
        Task SetPositionAsync(ObjectPath TrackId, long Position);
        Task OpenUriAsync(string Uri);
        Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null);
        Task<object> GetAsync(string prop);
        Task<PlayerProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class PlayerProperties
    {
        public string PlaybackStatus
        {
            get;

            set;
        }

        public string LoopStatus
        {
            get;

            set;
        }

        public double Rate
        {
            get;

            set;
        }

        public bool Shuffle
        {
            get;

            set;
        }

        public IDictionary<string, object> Metadata
        {
            get;

            set;
        }

        public double Volume
        {
            get;

            set;
        }

        public long Position
        {
            get;

            set;
        }

        public double MinimumRate
        {
            get;

            set;
        }

        public double MaximumRate
        {
            get;

            set;
        }

        public bool CanGoNext
        {
            get;

            set;
        }

        public bool CanGoPrevious
        {
            get;

            set;
        }

        public bool CanPlay
        {
            get;

            set;
        }=true;

        public bool CanPause
        {
            get;

            set;
        }=true;

        public bool CanSeek
        {
            get;

            set;
        }=true;

        public bool CanControl
        {
            get;

            set;
        }=true;
    }


    [DBusInterface("org.mpris.MediaPlayer2.Playlists")]
    interface IPlaylists : IDBusObject
    {
        Task ActivatePlaylistAsync(ObjectPath PlaylistId);
        Task<(ObjectPath, string, string)[]> GetPlaylistsAsync(uint Index, uint MaxCount, string Order, bool ReverseOrder);
        Task<IDisposable> WatchPlaylistChangedAsync(Action<(ObjectPath playlist, string, string)> handler, Action<Exception> onError = null);
        Task<object> GetAsync(string prop);
        Task<PlaylistsProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class PlaylistsProperties
    {
        public uint PlaylistCount
        {
            get;

            set;
        }

        public string[] Orderings
        {
            get;

            set;
        }

        public (bool, (ObjectPath, string, string)) ActivePlaylist
        {
            get;

            set;
        }
    }



    [DBusInterface("org.mpris.MediaPlayer2.TrackList")]
    interface ITrackList : IDBusObject
    {
        Task<IDictionary<string, object>[]> GetTracksMetadataAsync(ObjectPath[] TrackIds);
        Task AddTrackAsync(string Uri, ObjectPath AfterTrack, bool SetAsCurrent);
        Task RemoveTrackAsync(ObjectPath TrackId);
        Task GoToAsync(ObjectPath TrackId);
        Task<IDisposable> WatchTrackListReplacedAsync(Action<(ObjectPath[] tracks, ObjectPath currentTrack)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTrackAddedAsync(Action<(IDictionary<string, object> metadata, ObjectPath afterTrack)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTrackRemovedAsync(Action<ObjectPath> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTrackMetadataChangedAsync(Action<(ObjectPath trackId, IDictionary<string, object> metadata)> handler, Action<Exception> onError = null);
        Task<object> GetAsync(string prop);
        Task<TrackListProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class TrackListProperties
    {
        public ObjectPath[] Tracks
        {
            get;

            set;
        }

        public bool CanEditTracks
        {
            get;

            set;
        }
    }



    [DBusInterface("org.mpris.MediaPlayer2")]
    interface IMediaPlayer2 : IDBusObject
    {
        Task RaiseAsync();
        Task QuitAsync();
        Task<object> GetAsync(string prop);
        Task<MediaPlayer2Properties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    public class MediaPlayer2Properties
    {
        public bool CanQuit
        {
            get;

            set;
        }

        public bool Fullscreen
        {
            get;

            set;
        }

        public bool CanSetFullscreen
        {
            get;

            set;
        }

        public bool CanRaise
        {
            get;

            set;
        }

        public bool HasTrackList
        {
            get;

            set;
        }

        public string Identity
        {
            get;

            set;
        }

        public string DesktopEntry
        {
            get;

            set;
        }

        public string[] SupportedUriSchemes
        {
            get;

            set;
        }

        public string[] SupportedMimeTypes
        {
            get;
            set;
        }
    }


}
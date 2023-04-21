using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace SilverAudioPlayer.Linux.MPRIS
{
    [DBusInterface("org.mpris.MediaPlayer2")]
    public interface IMediaPlayer2 : IDBusObject
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
        private bool _canQuit = default(bool);
        public bool CanQuit
        {
            get
            {
                return _canQuit;
            }

            set
            {
                _canQuit = (value);
            }
        }

        private bool _canRaise = default(bool);
        public bool CanRaise
        {
            get
            {
                return _canRaise;
            }

            set
            {
                _canRaise = (value);
            }
        }

        private bool _hasTrackList = default(bool);
        public bool HasTrackList
        {
            get
            {
                return _hasTrackList;
            }

            set
            {
                _hasTrackList = (value);
            }
        }

        private string _identity = default(string);
        public string Identity
        {
            get
            {
                return _identity;
            }

            set
            {
                _identity = (value);
            }
        }

        private string _desktopEntry = default(string);
        public string DesktopEntry
        {
            get
            {
                return _desktopEntry;
            }

            set
            {
                _desktopEntry = (value);
            }
        }

        private string[] _supportedUriSchemes = default(string[]);
        public string[] SupportedUriSchemes
        {
            get
            {
                return _supportedUriSchemes;
            }

            set
            {
                _supportedUriSchemes = (value);
            }
        }

        private string[] _supportedMimeTypes = default(string[]);
        public string[] SupportedMimeTypes
        {
            get
            {
                return _supportedMimeTypes;
            }

            set
            {
                _supportedMimeTypes = (value);
            }
        }
    }

    

    [DBusInterface("org.mpris.MediaPlayer2.Player")]
    public interface IPlayer : IDBusObject
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
    public class PlayerProperties
    {
        private string _playbackStatus = default(string);
        public string PlaybackStatus
        {
            get
            {
                return _playbackStatus;
            }

            set
            {
                _playbackStatus = (value);
            }
        }

        private double _rate = default(double);
        public double Rate
        {
            get
            {
                return _rate;
            }

            set
            {
                _rate = (value);
            }
        }

        private IDictionary<string, object> _metadata = default(IDictionary<string, object>);
        public IDictionary<string, object> Metadata
        {
            get
            {
                return _metadata;
            }

            set
            {
                _metadata = (value);
            }
        }

        private double _volume = default(double);
        public double Volume
        {
            get
            {
                return _volume;
            }

            set
            {
                _volume = (value);
            }
        }

        private long _position = default(long);
        public long Position
        {
            get
            {
                return _position;
            }

            set
            {
                _position = (value);
            }
        }

        private double _minimumRate = default(double);
        public double MinimumRate
        {
            get
            {
                return _minimumRate;
            }

            set
            {
                _minimumRate = (value);
            }
        }

        private double _maximumRate = default(double);
        public double MaximumRate
        {
            get
            {
                return _maximumRate;
            }

            set
            {
                _maximumRate = (value);
            }
        }

        private bool _canGoNext = default(bool);
        public bool CanGoNext
        {
            get
            {
                return _canGoNext;
            }

            set
            {
                _canGoNext = (value);
            }
        }

        private bool _canGoPrevious = default(bool);
        public bool CanGoPrevious
        {
            get
            {
                return _canGoPrevious;
            }

            set
            {
                _canGoPrevious = (value);
            }
        }

        private bool _canPlay = default(bool);
        public bool CanPlay
        {
            get
            {
                return _canPlay;
            }

            set
            {
                _canPlay = (value);
            }
        }

        private bool _canPause = default(bool);
        public bool CanPause
        {
            get
            {
                return _canPause;
            }

            set
            {
                _canPause = (value);
            }
        }

        private bool _canSeek = default(bool);
        public bool CanSeek
        {
            get
            {
                return _canSeek;
            }

            set
            {
                _canSeek = (value);
            }
        }

        private bool _canControl = default(bool);
        public bool CanControl
        {
            get
            {
                return _canControl;
            }

            set
            {
                _canControl = (value);
            }
        }
    }

   
}
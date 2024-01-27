using System.Runtime.CompilerServices;
using Tmds.DBus;
//https://specifications.freedesktop.org/mpris-spec/latest/
[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace DBusInterop
{
    [DBusInterface("org.mpris.MediaPlayer2.Player")]
    interface IPlayer : IDBusObject
    {
        Task NextAsync();
        Task PauseAsync();
        Task PlayAsync();
        Task PlayPauseAsync();
        Task PreviousAsync();
        Task SeekAsync(long Offset);
        Task SetPositionAsync(ObjectPath TrackId, long Position);
        Task StopAsync();
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
        private bool _CanControl = default(bool);
        public bool CanControl
        {
            get
            {
                return _CanControl;
            }

            set
            {
                _CanControl = (value);
            }
        }

        private bool _CanGoNext = default(bool);
        public bool CanGoNext
        {
            get
            {
                return _CanGoNext;
            }

            set
            {
                _CanGoNext = (value);
            }
        }

        private bool _CanGoPrevious = default(bool);
        public bool CanGoPrevious
        {
            get
            {
                return _CanGoPrevious;
            }

            set
            {
                _CanGoPrevious = (value);
            }
        }

        private bool _CanPause = default(bool);
        public bool CanPause
        {
            get
            {
                return _CanPause;
            }

            set
            {
                _CanPause = (value);
            }
        }

        private bool _CanPlay = default(bool);
        public bool CanPlay
        {
            get
            {
                return _CanPlay;
            }

            set
            {
                _CanPlay = (value);
            }
        }

        private bool _CanSeek = default(bool);
        public bool CanSeek
        {
            get
            {
                return _CanSeek;
            }

            set
            {
                _CanSeek = (value);
            }
        }

        private IDictionary<string, object> _Metadata = default(IDictionary<string, object>);
        public IDictionary<string, object> Metadata
        {
            get
            {
                return _Metadata;
            }

            set
            {
                _Metadata = (value);
            }
        }

        private string _PlaybackStatus = default(string);
        public string PlaybackStatus
        {
            get
            {
                return _PlaybackStatus;
            }

            set
            {
                _PlaybackStatus = (value);
            }
        }

        private long _Position = default(long);
        public long Position
        {
            get
            {
                return _Position;
            }

            set
            {
                _Position = (value);
            }
        }

        private double _Volume = default(double);
        public double Volume
        {
            get
            {
                return _Volume;
            }

            set
            {
                _Volume = (value);
            }
        }
    }

   /* static class PlayerExtensions
    {
        public static Task<bool> GetCanControlAsync(this IPlayer o) => o.GetAsync<bool>("CanControl");
        public static Task<bool> GetCanGoNextAsync(this IPlayer o) => o.GetAsync<bool>("CanGoNext");
        public static Task<bool> GetCanGoPreviousAsync(this IPlayer o) => o.GetAsync<bool>("CanGoPrevious");
        public static Task<bool> GetCanPauseAsync(this IPlayer o) => o.GetAsync<bool>("CanPause");
        public static Task<bool> GetCanPlayAsync(this IPlayer o) => o.GetAsync<bool>("CanPlay");
        public static Task<bool> GetCanSeekAsync(this IPlayer o) => o.GetAsync<bool>("CanSeek");
        public static Task<IDictionary<string, object>> GetMetadataAsync(this IPlayer o) => o.GetAsync<IDictionary<string, object>>("Metadata");
        public static Task<string> GetPlaybackStatusAsync(this IPlayer o) => o.GetAsync<string>("PlaybackStatus");
        public static Task<long> GetPositionAsync(this IPlayer o) => o.GetAsync<long>("Position");
        public static Task<double> GetVolumeAsync(this IPlayer o) => (double)o.GetAsync("Volume");
        public static Task SetVolumeAsync(this IPlayer o, double val) => o.SetAsync("Volume", val);
    }*/

    [DBusInterface("org.mpris.MediaPlayer2")]
    interface IMediaPlayer2 : IDBusObject
    {
        Task QuitAsync();
        Task RaiseAsync();
        Task<object> GetAsync(string prop);
        Task<MediaPlayer2Properties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class MediaPlayer2Properties
    {
        private bool _CanQuit = default(bool);
        public bool CanQuit
        {
            get
            {
                return _CanQuit;
            }

            set
            {
                _CanQuit = (value);
            }
        }

        private bool _CanRaise = default(bool);
        public bool CanRaise
        {
            get
            {
                return _CanRaise;
            }

            set
            {
                _CanRaise = (value);
            }
        }

        private string _DesktopEntry = default(string);
        public string DesktopEntry
        {
            get
            {
                return _DesktopEntry;
            }

            set
            {
                _DesktopEntry = (value);
            }
        }

        private string _Identity = default(string);
        public string Identity
        {
            get
            {
                return _Identity;
            }

            set
            {
                _Identity = (value);
            }
        }

        private string[] _SupportedUriSchemes = default(string[]);
        public string[] SupportedUriSchemes
        {
            get
            {
                return _SupportedUriSchemes;
            }

            set
            {
                _SupportedUriSchemes = (value);
            }
        }

        private string[] _SupportedMimeTypes = default(string[]);
        public string[] SupportedMimeTypes
        {
            get
            {
                return _SupportedMimeTypes;
            }

            set
            {
                _SupportedMimeTypes = (value);
            }
        }
    }
/*
    static class MediaPlayer2Extensions
    {
        public static Task<bool> GetCanQuitAsync(this IMediaPlayer2 o) => o.GetAsync<bool>("CanQuit");
        public static Task<bool> GetCanRaiseAsync(this IMediaPlayer2 o) => o.GetAsync<bool>("CanRaise");
        public static Task<string> GetDesktopEntryAsync(this IMediaPlayer2 o) => o.GetAsync<string>("DesktopEntry");
        public static Task<string> GetIdentityAsync(this IMediaPlayer2 o) => o.GetAsync<string>("Identity");
        public static Task<string[]> GetSupportedUriSchemesAsync(this IMediaPlayer2 o) => o.GetAsync<string[]>("SupportedUriSchemes");
        public static Task<string[]> GetSupportedMimeTypesAsync(this IMediaPlayer2 o) => o.GetAsync<string[]>("SupportedMimeTypes");
    }*/
}
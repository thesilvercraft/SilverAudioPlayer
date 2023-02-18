using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace SilverAudioPlayer.Linux.MPRIS
{
    [DBusInterface("org.mpris.MediaPlayer2.SilverAudioPlayer")]
    interface IMediaPlayer2 : IDBusObject
    {
        Task RaiseAsync();
        Task QuitAsync();
        Task<object> GetAsync(string prop);
        Task<MediaPlayer2Properties> GetAllAsync();
        Task SetAsync(string prop, object val);
    }

    [Dictionary]
    public class MediaPlayer2Properties
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

        private bool _HasTrackList = default(bool);
        public bool HasTrackList
        {
            get
            {
                return _HasTrackList;
            }

            set
            {
                _HasTrackList = (value);
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

    static class MediaPlayer2Extensions
    {
        public static async Task<bool> GetCanQuitAsync(this IMediaPlayer2 o) => (bool)await o.GetAsync("CanQuit");
        public static async Task<bool> GetCanRaiseAsync(this IMediaPlayer2 o) => (bool)await o.GetAsync("CanRaise");
        public static async Task<bool> GetHasTrackListAsync(this IMediaPlayer2 o) => (bool)await o.GetAsync("HasTrackList");
        public static async Task<string> GetIdentityAsync(this IMediaPlayer2 o) => (string)await o.GetAsync("Identity");
        public static async Task<string> GetDesktopEntryAsync(this IMediaPlayer2 o) => (string)await o.GetAsync("DesktopEntry");
        public static async Task<string[]> GetSupportedUriSchemesAsync(this IMediaPlayer2 o) => (string[])await o.GetAsync("SupportedUriSchemes");
        public static async Task<string[]> GetSupportedMimeTypesAsync(this IMediaPlayer2 o) => (string[])await o.GetAsync("SupportedMimeTypes");
    }

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
    }

    [Dictionary]
    class PlayerProperties
    {
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

        private double _Rate = default(double);
        public double Rate
        {
            get
            {
                return _Rate;
            }

            set
            {
                _Rate = (value);
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

        private double _MinimumRate = default(double);
        public double MinimumRate
        {
            get
            {
                return _MinimumRate;
            }

            set
            {
                _MinimumRate = (value);
            }
        }

        private double _MaximumRate = default(double);
        public double MaximumRate
        {
            get
            {
                return _MaximumRate;
            }

            set
            {
                _MaximumRate = (value);
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
    }

    static class PlayerExtensions
    {
        public static async Task<string> GetPlaybackStatusAsync(this IPlayer o) => (string)await o.GetAsync("PlaybackStatus");
        public static async Task<double> GetRateAsync(this IPlayer o) => (double)await o.GetAsync("Rate");
        public static async Task<IDictionary<string, object>> GetMetadataAsync(this IPlayer o) =>(IDictionary<string, object>)await o.GetAsync("Metadata");
        public static async Task<double> GetVolumeAsync(this IPlayer o) => (double)await o.GetAsync("Volume");
        public static async Task<long> GetPositionAsync(this IPlayer o) => (long)await o.GetAsync("Position");
        public static async Task<double> GetMinimumRateAsync(this IPlayer o) => (double)await o.GetAsync("MinimumRate");
        public static async Task<double> GetMaximumRateAsync(this IPlayer o) => (double)await o.GetAsync("MaximumRate");
        public static async Task<bool> GetCanGoNextAsync(this IPlayer o) => (bool)await o.GetAsync("CanGoNext");
        public static async  Task<bool> GetCanGoPreviousAsync(this IPlayer o) => (bool)await o.GetAsync("CanGoPrevious");
        public static async Task<bool> GetCanPlayAsync(this IPlayer o) => (bool)await o.GetAsync("CanPlay");
        public static async Task<bool> GetCanPauseAsync(this IPlayer o) => (bool)await o.GetAsync("CanPause");
        public static async Task<bool> GetCanSeekAsync(this IPlayer o) => (bool)await o.GetAsync("CanSeek");
        public static async Task<bool> GetCanControlAsync(this IPlayer o) =>  (bool)await o.GetAsync("CanControl");
        public static Task SetRateAsync(this IPlayer o, double val) => o.SetAsync("Rate", val);
        public static Task SetVolumeAsync(this IPlayer o, double val) => o.SetAsync("Volume", val);
    }
}
using GenHTTP.Engine;
using GenHTTP.Modules.IO;
using GoogleCast;
using GoogleCast.Channels;
using GoogleCast.Models.Media;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;
using System.Net.Sockets;
using System.Net;
using GenHTTP.Modules.Practices;
using System.ComponentModel;

namespace SilverAudioPlayer.Any.PlayProvider.ChromeCast
{
    public class ChromeCastSettings: INotifyPropertyChanged,ICanBeToldThatAPartOfMeIsChanged
    {
        public Guid ChromeCast { get; set; }

        public bool AllowedToRead => throw new NotImplementedException();

        public event PropertyChangedEventHandler? PropertyChanged;

        void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }
    }

    [Export(typeof(IPlayProvider))]

    public class ChromeCastPlayProvider : IPlayProvider,IAmOnceAgainAskingYouForYourMemory
    {
        public ChromeCastPlayProvider()
        {
            var a = new ChromeCastSettings();
            _ObjectsToRememberForMe = new ObjectToRemember[] { new(new("443f9f7e-9cfe-4f7c-a6d5-edced58542ea"),a) };
        }

        public IReadOnlyList<MimeType>? SupportedMimes =>new MimeType[] { KnownMimes.AACMime, KnownMimes.WAVMime, KnownMimes.FLACMime,KnownMimes.MP3Mime,KnownMimes.OGGMime };

        public IPlayProviderListner ProviderListner { set => _=value; }

        public string Name => "Chromecast play provider - POC";

        public string Description => "Provides players by spinning up http servers and many other things to chromecast";

        public WrappedStream? Icon => null;

        public Version? Version => typeof(ChromeCastPlayProvider).Assembly.GetName().Version;

        public string Licenses => "GPL3.0";

        public List<Tuple<Uri, URLType>>? Links => new() { 
        new(new Uri("https://www.nuget.org/packages/GoogleCast"), URLType.PackageManager),
        new(new Uri("https://github.com/kakone/GoogleCast"), URLType.LibraryCode),

        };
        ObjectToRemember[] _ObjectsToRememberForMe;
        public ObjectToRemember[] ObjectsToRememberForMe => _ObjectsToRememberForMe;

        public bool CanPlayFile(WrappedStream stream)
        {
            stream.GetStream();
            return (SupportedMimes.Contains(stream.MimeType));//TODO make sure user has chromecast
        }

        public IPlay? GetPlayer(WrappedStream stream)
        {
            var chromecast= Task.Run(async () => { return await (new DeviceLocator().FindReceiversAsync()); }).GetAwaiter().GetResult().First();
            var sender = new Sender();
            Task.Run(async () =>
            {
                await sender.ConnectAsync(chromecast);
            }).GetAwaiter().GetResult();
            return new ChromeCastPlayer(stream,sender ,chromecast) ;
        }

        public Task OnStartup()
        {
            return Task.CompletedTask;
        }
    }
    public class ChromeCastPlayer : IPlay
    {
        IMediaChannel? mediaChannel = null;
        MediaStatus? state = null;
        GenHTTP.Modules.IO.Providers.ContentProviderBuilder? content = null;
        GenHTTP.Api.Infrastructure.IServerHost? server = null;
        public ChromeCastPlayer(WrappedStream stream, Sender sender, IReceiver chromecast) 
        {
            mediaChannel = sender.GetChannel<IMediaChannel>();
            Task.Run(async () => { 
                await sender.LaunchAsync(mediaChannel);
                string videostring;
                server?.Stop();
                void FromFile(string file)
                {
                    content = Content.From(Resource.FromFile(file));

                    server = Host.Create()
                                   .Handler(content)
                                   .Defaults(rangeSupport: true)
                                   .Start();

                    videostring = "http://" + getIPv4(chromecast.IPEndPoint.Address.ToString()) + ":8080/" + Guid.NewGuid(); //comment out if static
                }
                if (stream is WrappedFileStream wfs)
                {
                    FromFile(wfs.URL);
                }
                else if (stream is WrappedHttpStream whs)
                {
                    videostring = whs.Url;
                }
                else
                {
                    var filename = Path.GetTempFileName();
                    using var newFile = File.OpenWrite(filename);
                    stream.GetStream().CopyTo(newFile);
                    FromFile(filename);
                }
                var mediaStatus = await mediaChannel.LoadAsync(
            new MediaInformation() { ContentId = videostring },autoPlay:false);
                mediaChannel.StatusChanged += MediaChannel_StatusChanged;
            }).GetAwaiter().GetResult();
        }

        private void MediaChannel_StatusChanged(object? sender, EventArgs e)
        {
            Task.Run(() => {
              
                if (mediaChannel == null || mediaChannel.Status.Count()==0||  mediaChannel.Status.First().PlayerState =="STOPPED")
                {
                    TrackEnd?.Invoke(sender, e);
                }
                else if (mediaChannel.Status.First().PlayerState == "PAUSED")
                {
                    TrackPause?.Invoke(sender, e);

                }
            });
           
           
        }

        string? getIPv4(string chromecast)
        {
            try
            {
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect(chromecast, 1337);
                IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address?.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }
        public event EventHandler<object> TrackEnd;
        public event EventHandler<object> TrackPause;
        public MediaStatus? GetMediaChannelStatus()
        {
            return Task.Run(async () =>
            {
                try
                {
                    var s = await mediaChannel?.GetStatusAsync();
                    return s;

                }
                catch
                {
                    return null;
                }
            }).GetAwaiter().GetResult();
        }
        public uint? ChannelCount() => null;
      

        public uint? GetBitsPerSample() => null;

        public PlaybackState? GetPlaybackState()
        {
            if(mediaChannel==null)
            {
                return PlaybackState.Stopped;
            }
            return GetMediaChannelStatus()?.PlayerState switch
                {
                    "STOPPED" => PlaybackState.Stopped,
                    "PAUSED" => PlaybackState.Paused,
                    "PLAYING" => PlaybackState.Playing,
                    "BUFFERING" => PlaybackState.Buffering,
                    _ => PlaybackState.Playing,
                } ;

        }

        public TimeSpan GetPosition()
        {
            if (mediaChannel == null)
            {
                return TimeSpan.FromSeconds(2);
            }
            return TimeSpan.FromSeconds(GetMediaChannelStatus()?.CurrentTime ?? 2);

        }
      
        public long? GetSampleRate() => null;
        public TimeSpan? Length()
        {
            if (mediaChannel == null)
            {
                return TimeSpan.FromSeconds(3);
            }
            return TimeSpan.FromSeconds(GetMediaChannelStatus()?.Media.Duration.Value ??3);
        }

        public void Pause()
        {
           Task.Run(async ()=> { await mediaChannel?.PauseAsync(); });

        }

        public void Play()
        {
            Task.Run(async () => {
                await mediaChannel?.PlayAsync();
            });
        }

        public void Resume()
        {
                Task.Run(async () => {
                    await mediaChannel?.PlayAsync();
                });
        }

        public void SetPosition(TimeSpan position)
        {
                    Task.Run(async () => {
                        await mediaChannel?.SeekAsync(position.TotalSeconds);
                    });
        }

        public void SetVolume(byte volume)
        {
        }

        public void Stop()
        {
                        Task.Run(async () => {
                            await mediaChannel?.StopAsync();
                        });
            server?.Stop();

        }
    }
}
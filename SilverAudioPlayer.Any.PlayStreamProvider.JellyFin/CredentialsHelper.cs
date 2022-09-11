using Jellyfin.Sdk;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Diagnostics;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
{
    public class JellyFinHelper
    {
        public JellyFinHelper()
        {
            settings = new();
            settings.ClientVersion = "0";
            settings.ClientName = "SilverAudioPlayer.PlayStreamProvider.JellyFin";
            settings.DeviceName = Environment.MachineName;
            settings.DeviceId = "1";
            //Leave deviceid as is for privacy?
            systemClient = new SystemClient(settings, HttpClient.Client);
            userViewsClient = new UserViewsClient(settings, HttpClient.Client);
            userLibraryClient = new UserLibraryClient(settings, HttpClient.Client);
            userClient = new UserClient(settings, HttpClient.Client);
            itemsClient = new ItemsClient(settings, HttpClient.Client);
            audioClient = new UniversalAudioClient(settings, HttpClient.Client);
            imageClient=new ImageClient(settings, HttpClient.Client);
        }

        public async Task<IReadOnlyList<BaseItemDto>> GetItemsFromItem(BaseItemDto dto)
        {
            var a = await itemsClient.GetItemsByUserIdAsync(userDto.Id, parentId: dto.Id);
            return a.Items;
        }

        internal async Task MakeSureUserLogsIn(Gui gui)
        {
            while (!validServer)
            {
                await GetServerUrl(gui);
            }
            while (!validUser)
            {
                await LogIn(gui);
            }

            //password dialog here
        }

        public async Task<IReadOnlyList<BaseItemDto>> GetDefaultItems()
        {
            var views = await userViewsClient.GetUserViewsAsync(userDto.Id);
            return views.Items;
        }

        public async Task<bool> TryLogInAsync(string username, string password)
        {
            try
            {
                var authenticationResult = await userClient.AuthenticateUserByNameAsync(new AuthenticateUserByName
                {
                    Username = username,
                    Pw = password
                }).ConfigureAwait(false);
                settings.AccessToken = authenticationResult.AccessToken;
                userDto = authenticationResult.User;
                validUser = true;
                return validUser;
            }
            catch (UserException ex)
            {
                await Console.Error.WriteLineAsync("Error authenticating.").ConfigureAwait(false);
                await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return false;
            }
        }

        private UserDto userDto;
        private bool validUser = false;
        private SdkClientSettings settings;
        private ISystemClient systemClient;
        private IUserViewsClient userViewsClient;
        private IUserLibraryClient userLibraryClient;
        private IUserClient userClient;
        private bool validServer = false;
        private IItemsClient itemsClient;
        private IUniversalAudioClient audioClient;

        public async Task<bool> TryGetSystemInfoAsync(string host)
        {
            validServer = false;
            settings.BaseUrl = host;
            try
            {
                var systemInfo = await systemClient.GetPublicSystemInfoAsync()
                    .ConfigureAwait(false);
                validServer = true;
                Debug.WriteLine($"Connected to {host}");
                Debug.WriteLine($"Server Name: {systemInfo.ServerName}");
                Debug.WriteLine($"Server Version: {systemInfo.Version}");
                return true;
            }
            catch (InvalidOperationException ex)
            {
                await Console.Error.WriteLineAsync("Invalid url").ConfigureAwait(false);
                await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            }
            catch (Jellyfin.Sdk.SystemException ex)
            {
                await Console.Error.WriteLineAsync($"Error connecting to {host}").ConfigureAwait(false);
                await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            }
            return false;
        }

        public async Task<WrappedStream> GetStream(BaseItemDto dto)
        {
            return new WrappedJellyFinStream(audioClient, userDto, dto);
        }
        public async Task<WrappedStream?> GetImageStream(BaseItemDto dto)
        {
            try
            {
                var fr = await imageClient.GetItemImageAsync(dto.Id, ImageType.Primary);

                return new WrappedStreamImplementedByOneRealOne(KnownMimes.JPGMime, fr.Stream);
            }
       catch
            {
                return null;
            }
        }
        IImageClient imageClient;
        private ServerUrlWindow serverwindow;
        private AuthInfoWindow authwindow;

        public async Task<bool> LogIn(Gui gui)
        {
            if (authwindow == null)
            {
                authwindow = new(this);
            }
            await authwindow.ShowDialog(gui);
            Debug.WriteLine("exit window");
            return serverwindow.Success;
        }

        public async Task<bool> GetServerUrl(Gui gui)
        {
            if (serverwindow == null)
            {
                serverwindow = new(this);
            }
            await serverwindow.ShowDialog(gui);

            Debug.WriteLine("exit window");
            return serverwindow.Success;
        }
    }
    public  class WrappedStreamImplementedByOneRealOne:WrappedStream
    {
        public WrappedStreamImplementedByOneRealOne(MimeType mimeType, Stream RealStream)
        {
            MimeType = mimeType;
        }

        public override MimeType MimeType { get; }
        Stream RealStream;
        public override Stream GetStream()
        {
            return RealStream;
        }

    }
    public class WrappedJellyFinStream : WrappedStream, IDisposable
    {
        private bool disposedValue;
        private IUniversalAudioClient audioClient;
        private UserDto userDto;
        private BaseItemDto song;
        private MemoryStream FStream = new();
        private Stream rs;
        private Thread t;

        private void CopyToMS()
        {
            try
            {
                rs.CopyTo(FStream);
            }
            catch
            {

            }
        }

        public WrappedJellyFinStream(IUniversalAudioClient ac, UserDto user, BaseItemDto baseItemDto)
        {
            audioClient = ac;
            song = baseItemDto;
            userDto = user;

            userDto = user;
            audioClient = ac;

            dto = baseItemDto;
        }

        private BaseItemDto dto;

        public List<Stream> Streams { get; set; } = new();
        public override MimeType MimeType { get => _MimeType; }
        private MimeType _MimeType { get; set; }

        private Stream InternalGetStream()
        {
            var Stream = audioClient.GetUniversalAudioStreamAsync(song.Id, container: new string[] { "flac", "wav", "mp3" }, userId: userDto.Id).GetAwaiter().GetResult();
            Streams.Add(Stream.Stream);
            return Stream.Stream;
        }

       

        public override Stream GetStream()
        {
            var content = audioClient.GetUniversalAudioStreamAsync(song.Id, container: new string[] { "flac", "wav", "mp3" }, userId: userDto.Id).GetAwaiter().GetResult();
            var Stream = content.Stream;
            Streams.Add(Stream);
            
            MimeType? mt = KnownMimes.GetKnownMimeByName(content.Headers["Content-Type"].First());
            if (mt == null)
            {
                var stream2 = InternalGetStream();
                try
                {
                    mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
                }
                finally
                {
                    stream2.Dispose();
                    Streams.Remove(stream2);
                }
            }
            
            _MimeType = mt;

            rs = Stream;
            if(!FStream.CanRead)
            {
                FStream = new();

            }
            if (FStream.Length == 0)
            {
                t = new(CopyToMS);
                t.Start();
            }
            return new FakeMemoryStreamReference(FStream);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var stream in Streams.Where(x => x.CanRead))
                    {
                        stream.Dispose();
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class FakeMemoryStreamReference : Stream, IDisposable
    {
        private MemoryStream Realstream;
        private MemoryStream Fakestream;

        public FakeMemoryStreamReference(MemoryStream realstream)
        {
            Realstream = realstream;
            Fakestream = new MemoryStream(realstream.ToArray());
        }

        public override bool CanRead => Fakestream.CanRead;

        public override bool CanSeek => Fakestream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => Fakestream.Length;
        public long FakePos;
        public override long Position { get => Fakestream.Position; set => Fakestream.Position = value; }

        public override void Flush()
        {
            Realstream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Fakestream.Length != Realstream.Length)
            {
                FakePos = Fakestream.Position;
                Fakestream = new(Realstream.GetBuffer());
                Fakestream.Position = FakePos;
            }
            var a = Fakestream.Read(buffer, offset, count);
            FakePos += a;
            return a;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Fakestream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        void IDisposable.Dispose()
        {
            Fakestream.Dispose();
            Realstream.Dispose();
            GC.SuppressFinalize(this);
            base.Dispose();
        }
    }
}
using System.Diagnostics;
using Jellyfin.Sdk;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;
using SystemException = Jellyfin.Sdk.SystemException;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class JellyFinHelper
{
    private readonly IUniversalAudioClient audioClient;
    private AuthInfoWindow authwindow;
    private readonly IImageClient imageClient;
    private readonly IItemsClient itemsClient;
    private ServerUrlWindow serverwindow;
    private readonly SdkClientSettings settings;
    private readonly ISystemClient systemClient;
    private readonly IUserClient userClient;

    private UserDto userDto;
    private IUserLibraryClient userLibraryClient;
    private readonly IUserViewsClient userViewsClient;
    private bool validServer;
    private bool validUser;

    public JellyFinHelper()
    {
        settings = new SdkClientSettings();
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
        imageClient = new ImageClient(settings, HttpClient.Client);
    }

    public async Task<IReadOnlyList<BaseItemDto>> GetItemsFromItem(BaseItemDto dto)
    {
        var a = await itemsClient.GetItemsByUserIdAsync(userDto.Id, parentId: dto.Id);
        return a.Items;
    }

    internal async Task MakeSureUserLogsIn(Gui gui)
    {
        while (!validServer) await GetServerUrl(gui);
        while (!validUser) await LogIn(gui);

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
        catch (SystemException ex)
        {
            await Console.Error.WriteLineAsync($"Error connecting to {host}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<WrappedStream> GetStream(BaseItemDto dto)
    {
       // return new WrappedJellyFinStream(audioClient, userDto, dto);
       var goofyah = audioClient.GetUniversalAudioStreamUrl(dto.Id, deviceId: "1", userId: userDto.Id);
       Debug.WriteLine(goofyah);

        return new WrappedSusHttpStream(() =>
        {
            var request = new HttpRequestMessage() {
                RequestUri = new Uri(goofyah),
                Method = HttpMethod.Get,
            };
            request.Headers.Add("X-Emby-Token",settings.AccessToken);
            return HttpClient.
                Client.SendAsync(request);
        });
    }

    public async Task<WrappedStream?> GetImageStream(BaseItemDto dto)
    {
        try
        {
            var fr =  imageClient.GetItemImageUrl(dto.Id, ImageType.Primary);
            Debug.WriteLine(fr);
            return new WrappedSusHttpStream(() =>
            {
                var request = new HttpRequestMessage() {
                    RequestUri = new Uri(fr),
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("X-Emby-Token",settings.AccessToken);
                return HttpClient.
                    Client.SendAsync(request);
            });
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> LogIn(Gui gui)
    {
        if (authwindow == null) authwindow = new AuthInfoWindow(this);
        await authwindow.ShowDialog(gui);
        Debug.WriteLine("exit window");
        return serverwindow.Success;
    }

    public async Task<bool> GetServerUrl(Gui gui)
    {
        if (serverwindow == null) serverwindow = new ServerUrlWindow(this);
        await serverwindow.ShowDialog(gui);

        Debug.WriteLine("exit window");
        return serverwindow.Success;
    }
}

public class WrappedStreamImplementedByOneRealOne : WrappedStream
{
    private Stream RealStream;

    public WrappedStreamImplementedByOneRealOne(MimeType mimeType, Stream RealStream)
    {
        MimeType = mimeType;
    }

    public override MimeType MimeType { get; }
    public override bool ShouldDisposeStream => false;
    public override Stream GetStream()
    {
        return RealStream;
    }

    public override void Dispose()
    {
        RealStream.Dispose();
    }
}

public class WrappedJellyFinStream : WrappedStream, IDisposable
{
    private readonly IUniversalAudioClient audioClient;
    private bool disposedValue;

    private BaseItemDto dto;
    private MemoryStream FStream = new();
    private Stream rs;
    private readonly BaseItemDto song;
    private Thread t;
    private readonly UserDto userDto;

    public WrappedJellyFinStream(IUniversalAudioClient ac, UserDto user, BaseItemDto baseItemDto)
    {
        audioClient = ac;
        song = baseItemDto;
        userDto = user;

        userDto = user;
        audioClient = ac;

        dto = baseItemDto;
    }

    public List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; set; }

    public override void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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

    private Stream InternalGetStream()
    {
        var Stream = audioClient
            .GetUniversalAudioStreamAsync(song.Id, new[] { "flac", "wav", "mp3" }, userId: userDto.Id).GetAwaiter()
            .GetResult();
        Streams.Add(Stream.Stream);
        return Stream.Stream;
    }


    public override Stream GetStream()
    {
        var content = audioClient
            .GetUniversalAudioStreamAsync(song.Id, new[] { "flac", "wav", "mp3" }, userId: userDto.Id).GetAwaiter()
            .GetResult();
        var Stream = content.Stream;
        Streams.Add(Stream);

        var mt = KnownMimes.GetKnownMimeByName(content.Headers["Content-Type"].First());
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
        if (!FStream.CanRead) FStream = new MemoryStream();
        if (FStream.Length == 0)
        {
            t = new Thread(CopyToMS);
            t.Start();
        }

        return new FakeMemoryStreamReference(FStream);
    }

    public override bool ShouldDisposeStream => true;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
                foreach (var stream in Streams.Where(x => x.CanRead))
                    stream.Dispose();
            disposedValue = true;
        }
    }
}

public class FakeMemoryStreamReference : Stream, IDisposable
{
    public long FakePos;
    private MemoryStream Fakestream;
    private readonly MemoryStream Realstream;

    public FakeMemoryStreamReference(MemoryStream realstream)
    {
        Realstream = realstream;
        Fakestream = new MemoryStream(realstream.ToArray());
    }

    public override bool CanRead => Fakestream.CanRead;

    public override bool CanSeek => Fakestream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => Fakestream.Length;

    public override long Position
    {
        get => Fakestream.Position;
        set => Fakestream.Position = value;
    }

    void IDisposable.Dispose()
    {
        Fakestream.Dispose();
        Realstream.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    public override void Flush()
    {
        Realstream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Fakestream.Length != Realstream.Length)
        {
            FakePos = Fakestream.Position;
            Fakestream = new MemoryStream(Realstream.GetBuffer());
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
}
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Serilog;
using SilverAudioPlayer.Shared;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class JellyFinHelper
{
    public const string CacheFileName = "myapp_msal_cache.txt";
    private const string SilverAudioPlayerJellyfinGuid = "0b31c6c7-a28e-432a-8dfb-e34f56186225";
    public const string LinuxKeyRingSchema = "silvercraft.AudioPlayer.JellyFin";
    public const string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
    public const string LinuxKeyRingLabel = "SilverAudioPlayer jellyfin extension";
    private const string TraceSourceName = "SilverCraft.SilverAudioPlayer.JellyFin.CredentialsHelper";
    public static readonly string CacheDir = MsalCacheHelper.UserRootDirectory;
    public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new("Version", "1");
    public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new("ProductGroup", "MyApps");
    private readonly JellyfinSdkSettings settings;
    public JellyfinApiClient apiClient;
    private IPublicClientApplication app;
    private readonly JellyfinAuthenticationProvider jellyfinAuthentication;
    private readonly JellyfinRequestAdapter jellyfinRequestAdapter;
    private readonly ILogger logger;

    private UserDto userDto;
    private bool validServer;
    private bool validUser;

    public JellyFinHelper()
    {
        logger = Logger.GetLogger(typeof(JellyFinHelper)) ?? Log.Logger;

        settings = new JellyfinSdkSettings();
        settings.Initialize(
            "Catenoid",
            typeof(JellyFinHelper).Assembly.GetName().Version.ToString(),
            Environment.MachineName,
            "1");
        jellyfinAuthentication = new JellyfinAuthenticationProvider(settings);
        jellyfinRequestAdapter = new JellyfinRequestAdapter(jellyfinAuthentication, settings, HttpClient.Client);
        apiClient = new JellyfinApiClient(jellyfinRequestAdapter);
        app = PublicClientApplicationBuilder.Create(SilverAudioPlayerJellyfinGuid).Build();
        Task.Run(async () => await MoreLibSecretStuff()).GetAwaiter().GetResult();
    }

    public bool GetValidUser()
    {
        return validUser;
    }

    public async Task<IReadOnlyList<BaseItemDto>> SearchItems(string query)
    {
        var result = await apiClient.Items.GetAsync(configuration =>
        {
            configuration.QueryParameters.SearchTerm = query;
            configuration.QueryParameters.IncludeItemTypes = [BaseItemKind.Audio, BaseItemKind.MusicAlbum];
            configuration.QueryParameters.Recursive = true;
            configuration.QueryParameters.UserId = userDto?.Id;
        });
        return result.Items;
    }

    public async Task MoreLibSecretStuff()
    {
        var storage = Storage.Create(GetStorageCreationProperties());
        try
        {
            logger.Debug("Reading jellyfin login");
            var data = storage.ReadData();
            if (data.Length == 0)
            {
                logger.Verbose("No jellyfin login found");
                return;
            }

            var str = Encoding.UTF8.GetString(data);
            var logininfo = JsonSerializer.Deserialize<LoginInfo>(str);
            if (await TryGetSystemInfoAsync(logininfo.url))
            {
                settings.SetServerUrl(logininfo.url);
                settings.SetAccessToken(logininfo.accessToken);
                userDto = await apiClient.Users.Me.GetAsync();
                if (userDto != null)
                {
                    validUser = true;
                    logger.Verbose("Successfully got jellyfin user");
                }
                else
                {
                    settings.SetAccessToken(null);
                    storage.VerifyPersistence();
                    storage.Clear(true);
                    logger.Error("Could not get jellyfin user, clearing stored token");
                }
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Could not get jellyfin user, clearing stored token");
            storage.VerifyPersistence();
            storage.Clear(true);
            settings.SetAccessToken(null);
        }
    }

    private static StorageCreationProperties ConfigureSecureStorage(bool usePlaintextFileOnLinux)
    {
        if (!usePlaintextFileOnLinux)
            return new StorageCreationPropertiesBuilder(
                    CacheFileName,
                    CacheDir)
                .WithLinuxKeyring(
                    LinuxKeyRingSchema,
                    LinuxKeyRingCollection,
                    LinuxKeyRingLabel,
                    LinuxKeyRingAttr1,
                    LinuxKeyRingAttr2)
                .Build();
        return new StorageCreationPropertiesBuilder(
                CacheFileName +
                "plaintext", // do not use the same file name so as not to overwrite the encrypted version
                CacheDir)
            .WithLinuxUnprotectedFile()
            .Build();
    }

    private async Task<MsalCacheHelper> CreateCacheHelperAsync()
    {
        StorageCreationProperties storageProperties;
        MsalCacheHelper cacheHelper;
        try
        {
            storageProperties = ConfigureSecureStorage(false);
            cacheHelper = await MsalCacheHelper.CreateAsync(
                    storageProperties,
                    new TraceSource(TraceSourceName))
                .ConfigureAwait(false);

            // the underlying persistence mechanism might not be usable
            // this typically happens on Linux over SSH
            cacheHelper.VerifyPersistence();

            return cacheHelper;
        }
        catch (MsalCachePersistenceException ex)
        {
            logger.Error(ex, "Cannot persist data securely.");
            if (!SharedUtilities.IsLinuxPlatform()) throw;
            storageProperties = ConfigureSecureStorage(true);
            logger.Error(
                "Falling back on using a plaintext file located at {StoragePropertiesCacheFilePath} Users are responsible for securing this file!",
                storageProperties.CacheFilePath);
            cacheHelper = await MsalCacheHelper.CreateAsync(
                    storageProperties,
                    new TraceSource(TraceSourceName))
                .ConfigureAwait(false);
            return cacheHelper;
        }
    }

    public async Task<IReadOnlyList<BaseItemDto>> GetItemsFromItem(BaseItemDto dto)
    {
        var a = await apiClient.Items.GetAsync(configuration =>
            configuration.QueryParameters.ParentId =
                dto.Id); // itemsClient.GetItemsByUserIdAsync(userDto.Id, parentId: dto.Id)
        return a.Items;
    }

    internal async Task MakeSureUserLogsIn(Gui gui)
    {
        while (!validServer) await GetServerUrl(gui);
        while (!validUser) await LogIn(gui);
    }

    public async Task<IReadOnlyList<BaseItemDto>> GetDefaultViews()
    {
        var views = await apiClient.UserViews.GetAsync();
        return views.Items;
    }

    private StorageCreationProperties GetStorageCreationProperties()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(
                CacheFileName + ".other_secrets",
                CacheDir)
            .WithLinuxKeyring(
                LinuxKeyRingSchema,
                LinuxKeyRingCollection,
                LinuxKeyRingLabel,
                LinuxKeyRingAttr1,
                LinuxKeyRingAttr2);
        return storageProperties.Build();
    }

    public async Task<bool> TryLogInAsync(string username, string password)
    {
        try
        {
            var authenticationResult = await apiClient.Users.AuthenticateByName.PostAsync(new AuthenticateUserByName
                {
                    Username = username,
                    Pw = password
                })
                .ConfigureAwait(false);
            settings.SetAccessToken(authenticationResult.AccessToken);
            userDto = authenticationResult.User;
            var logininfo = JsonSerializer.Serialize(new LoginInfo(settings.ServerUrl, authenticationResult.AccessToken,
                userDto.Id.ToString()));
            var storage = Storage.Create(GetStorageCreationProperties());
            var secretBytes = Encoding.UTF8.GetBytes(logininfo);
            logger.Verbose("Writing jellyfin token");
            storage.WriteData(secretBytes);
            validUser = true;
            return validUser;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error authenticating");
            return false;
        }
    }

    public async Task RemoveTokenAsync()
    {
        var storage = Storage.Create(GetStorageCreationProperties());
        try
        {
            storage.Clear();
        }
        catch (Exception e)
        {
            logger.Error(e, "Error clearing token");
        }
    }

    public async Task<bool> TryGetSystemInfoAsync(string host)
    {
        validServer = false;
        settings.SetServerUrl(host);
        using var s_cts = new CancellationTokenSource();
        try
        {
            s_cts.CancelAfter(250); // if your jellyfin server is slower than that, you have other problems
            var systemInfo = await apiClient.System.Info.Public.GetAsync(cancellationToken: s_cts.Token)
                .ConfigureAwait(false);
            validServer = true;
            logger.Information("Connected to {Host}, name {SystemInfoServerName}, version {SystemInfoVersion} ", host,
                systemInfo.ServerName, systemInfo.Version);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            logger.Error(ex, "Invalid url");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error connecting to {Host}", host);
        }

        return false;
    }

    public async Task<WrappedStream> GetStream(BaseItemDto dto)
    {
        var baseUri = new Uri(settings.ServerUrl);
        var audioUri = new Uri(baseUri,
            $"/Audio/{dto.Id}/universal?UserId={userDto.Id}&DeviceId=1&MaxStreamingBitrate=140000000&Container=opus%2Cwebm%7Copus%2Cmp3%2Caac%2Cm4a%7Caac%2Cm4b%7Caac%2Cflac%2Cwebma%2Cwebm%7Cwebma%2Cwav%2Cogg&TranscodingContainer=ts&TranscodingProtocol=hls&AudioCodec=aac&api_key={settings.AccessToken}&PlaySessionId=1&StartTimeTicks=0&EnableRedirection=true&EnableRemoteMedia=false");
        return new WrappedHttpStream(audioUri.ToString());
    }

    public async Task<IReadOnlyList<BaseItemDto>> InstantMix(BaseItemDto dto)
    {
        if (dto.Id == null) throw new ArgumentException("Id is null");
        var instantMix = await apiClient.Items[(Guid)dto.Id].InstantMix.GetAsync();
        if (instantMix == null) throw new NullReferenceException("Instant mix is null");
        return instantMix?.Items;
    }

    public async Task<Tuple<WrappedStream, string>?> GetImageStreamAndId(BaseItemDto dto)
    {
        // If both are null, no image is available
        if (dto.ImageTags == null && dto.AlbumPrimaryImageTag == null)
            return null;

        string? desiredId = null;
        if (dto.ImageTags is { AdditionalData: not null })
            if (dto.ImageTags.AdditionalData.TryGetValue("Primary", out var value) && value is string idStr)
                desiredId = idStr;

        if (string.IsNullOrEmpty(desiredId)) desiredId = dto.AlbumPrimaryImageTag;

        if (string.IsNullOrEmpty(desiredId))
            return null;

        try
        {
            var ri = apiClient.Items[dto.Id.Value].Images[nameof(ImageType.Primary)].ToGetRequestInformation();
            var uri = apiClient.BuildUri(ri);
            var r = new HttpRequestMessage(HttpMethod.Get, uri);
            r.Headers.Add("X-Emby-Token", settings.AccessToken);
            return new Tuple<WrappedStream, string>(new WrappedHttpStream(r), desiredId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetImageStreamAndId: {ex}");
            return null;
        }
    }

    public async Task<bool> LogIn(Gui gui)
    {
        var authwindow = new AuthInfoWindow(this);
        await authwindow.ShowDialog(gui);
        return authwindow.Success;
    }

    public async Task<bool> GetServerUrl(Gui gui)
    {
        var serverwindow = new ServerUrlWindow(this);
        await serverwindow.ShowDialog(gui);
        return serverwindow.Success;
    }

    public async Task<IList<LyricPhrase>?> GetLyrics(BaseItemDto songDto)
    {
        try
        {
            var ri = await apiClient.Audio[songDto.Id.Value].Lyrics.GetAsync();
            var lyrics = ri.Lyrics.Select(x => new LyricPhrase(((double)x.Start / 10000)!, x.Text + "\n")).ToList();
            return lyrics;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private record class LoginInfo(string url, string accessToken, string userId);
}
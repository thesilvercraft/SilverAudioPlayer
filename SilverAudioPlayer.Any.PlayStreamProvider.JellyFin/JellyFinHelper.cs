using System.Diagnostics;
using System.Text;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using SilverAudioPlayer.Shared;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;
namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class JellyFinHelper
{
    private AuthInfoWindow authwindow;
    private ServerUrlWindow serverwindow;
    private readonly JellyfinSdkSettings settings;
   public JellyfinApiClient apiClient;

    private UserDto userDto;
    private bool validServer;
    private bool validUser;
    private IPublicClientApplication app;
    private JellyfinAuthenticationProvider jellyfinAuthentication;
    private JellyfinRequestAdapter  jellyfinRequestAdapter;
    public bool GetValidUser() => validUser;
    public JellyFinHelper()
    {
        settings = new JellyfinSdkSettings();
       settings.Initialize(
            "Catenoid",
             typeof(JellyFinHelper).Assembly.GetName().Version.ToString(),
             Environment.MachineName,
            $"1");
    
        jellyfinAuthentication = new JellyfinAuthenticationProvider(settings);
        jellyfinRequestAdapter = new JellyfinRequestAdapter(jellyfinAuthentication, settings, HttpClient.Client);
        apiClient=new(jellyfinRequestAdapter);
        app = PublicClientApplicationBuilder.Create(SilverAudioPlayerJellyfinGuid).Build();
        Task.Run(async ()=> await MoreLibSecretStuff()).GetAwaiter().GetResult();
    }
    public async Task<IReadOnlyList<BaseItemDto>> SearchItems(string query)
    {
        var result = await apiClient.Items.GetAsync(configuration => {
            configuration.QueryParameters.SearchTerm = query;
            configuration.QueryParameters.IncludeItemTypes = [BaseItemKind.Audio, BaseItemKind.MusicAlbum];
            configuration.QueryParameters.Recursive = true;
            configuration.QueryParameters.UserId = userDto?.Id;
        });
        return result.Items;
    }
    record class LoginInfo(string url, string accessToken, string userId);
    public async Task MoreLibSecretStuff()
    {
            var cacheHelper = await CreateCacheHelperAsync().ConfigureAwait(false);

            // 3. Let the cache helper handle MSAL's cache
            cacheHelper.RegisterCache(app.UserTokenCache);
          var storageProperties = new StorageCreationPropertiesBuilder(
               CacheFileName + ".other_secrets",
               CacheDir)
                .WithLinuxKeyring(
                                   LinuxKeyRingSchema,
                                   LinuxKeyRingCollection,
                                   LinuxKeyRingLabel,                                   
                                   LinuxKeyRingAttr1,
                                   new KeyValuePair<string, string>("other_secrets", "secret_description"));

            Storage storage = Storage.Create(storageProperties.Build());
            try
            {
                Debug.WriteLine("Reading...");
                var data = storage.ReadData();
                var str = Encoding.UTF8.GetString(data);
                Debug.WriteLine(str);
                var logininfo = System.Text.Json.JsonSerializer.Deserialize<LoginInfo>(str);
                if (await TryGetSystemInfoAsync(logininfo.url))
                {
                    settings.SetServerUrl(logininfo.url);
                    settings.SetAccessToken(logininfo.accessToken);
                    userDto = await apiClient.Users.Me.GetAsync();
                    if (userDto != null)
                    {
                        validUser = true;
                    }
                    else
                    {
                        settings.SetAccessToken(null);
                    }
                }
               
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                settings.SetAccessToken(null);
            }
    }
       private static StorageCreationProperties ConfigureSecureStorage(bool usePlaintextFileOnLinux)
        {
            if (!usePlaintextFileOnLinux)
            {
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
            }

            return new StorageCreationPropertiesBuilder(
                                     CacheFileName + "plaintext", // do not use the same file name so as not to overwrite the encypted version
                                     CacheDir)
                                 .WithLinuxUnprotectedFile()
                                 .Build();

        }
           private const string TraceSourceName = "SilverCraft.SilverAudioPlayer.JellyFin.CredentialsHelper";
        private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
        {
            StorageCreationProperties storageProperties;
            MsalCacheHelper cacheHelper;
            try
            {
                storageProperties = ConfigureSecureStorage(usePlaintextFileOnLinux: false);
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
                Console.WriteLine("Cannot persist data securely. ");
                Console.WriteLine("Details: " + ex);


                if (SharedUtilities.IsLinuxPlatform())
                {
                    storageProperties = ConfigureSecureStorage(usePlaintextFileOnLinux: true);

                    Console.WriteLine($"Falling back on using a plaintext " +
                        $"file located at {storageProperties.CacheFilePath} Users are responsible for securing this file!");

                    cacheHelper = await MsalCacheHelper.CreateAsync(
                           storageProperties,
                           new TraceSource(TraceSourceName))
                        .ConfigureAwait(false);

                    return cacheHelper;
                }
                throw;
            }
        }
    public const string CacheFileName = "myapp_msal_cache.txt";
     public readonly static string CacheDir = MsalCacheHelper.UserRootDirectory;
   const string SilverAudioPlayerJellyfinGuid="0b31c6c7-a28e-432a-8dfb-e34f56186225";
           public const string LinuxKeyRingSchema = "silvercraft.AudioPlayer.JellyFin";
        public const string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        public const string LinuxKeyRingLabel = "SilverAudioPlayer jellyfin extension";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "MyApps");
   
    public async Task<IReadOnlyList<Jellyfin.Sdk.Generated.Models.BaseItemDto>> GetItemsFromItem(Jellyfin.Sdk.Generated.Models.BaseItemDto dto)
    {
        var a = await apiClient.Items.GetAsync(configuration => configuration.QueryParameters.ParentId= dto.Id ); // itemsClient.GetItemsByUserIdAsync(userDto.Id, parentId: dto.Id)
        return a.Items;
    }

    internal async Task MakeSureUserLogsIn(Gui gui)
    {
        while (!validServer) await GetServerUrl(gui);
        while (!validUser) await LogIn(gui);
    }

    public async Task<IReadOnlyList<Jellyfin.Sdk.Generated.Models.BaseItemDto>> GetDefaultViews()
    {
        var views = await apiClient.UserViews.GetAsync();
        return views.Items;
    }

    public async Task<bool> TryLogInAsync(string username, string password)
    {
        try
        {
            var authenticationResult = await apiClient.Users.AuthenticateByName.PostAsync(new AuthenticateUserByName
                {
                    Username = username,
                    Pw = password,
                })
                .ConfigureAwait(false);
            settings.SetAccessToken(authenticationResult.AccessToken);
            userDto = authenticationResult.User;
            var logininfo = System.Text.Json.JsonSerializer.Serialize(new LoginInfo(settings.ServerUrl, authenticationResult.AccessToken, userDto.Id.ToString()));
            var storageProperties = new StorageCreationPropertiesBuilder(
                    CacheFileName + ".other_secrets",
                    CacheDir)
                .WithLinuxKeyring(
                    LinuxKeyRingSchema,
                    LinuxKeyRingCollection,
                    LinuxKeyRingLabel,                                   
                    LinuxKeyRingAttr1,
                    new KeyValuePair<string, string>("other_secrets", "secret_description"));

            Storage storage = Storage.Create(storageProperties.Build());
            byte[] secretBytes = Encoding.UTF8.GetBytes(logininfo);
            Debug.WriteLine("Writing...");
            storage.WriteData(secretBytes);
            validUser = true;
            return validUser;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync("Error authenticating.").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return false;
        }
    }

    public async Task<bool> TryGetSystemInfoAsync(string host)
    {
        validServer = false;
        settings.SetServerUrl(host);
        try
        {
            var systemInfo = await apiClient.System.Info.Public.GetAsync()
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
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error connecting to {host}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<WrappedStream> GetStream(Jellyfin.Sdk.Generated.Models.BaseItemDto dto)
    {
       // return new WrappedJellyFinStream(audioClient, userDto, dto);
      // var goofyah = audioClient.GetUniversalAudioStreamUrl(dto.Id, deviceId: "1", userId: userDto.Id);
      // Debug.WriteLine(goofyah);
        var baseUri = new Uri(settings.ServerUrl);
        var audioUri = new Uri(baseUri, $"/Audio/{dto.Id}/universal?UserId={userDto.Id}&DeviceId=1&MaxStreamingBitrate=140000000&Container=opus%2Cwebm%7Copus%2Cmp3%2Caac%2Cm4a%7Caac%2Cm4b%7Caac%2Cflac%2Cwebma%2Cwebm%7Cwebma%2Cwav%2Cogg&TranscodingContainer=ts&TranscodingProtocol=hls&AudioCodec=aac&api_key={settings.AccessToken}&PlaySessionId=1&StartTimeTicks=0&EnableRedirection=true&EnableRemoteMedia=false");
        Debug.WriteLine(audioUri);
        return new WrappedHttpStream(audioUri.ToString());
    }

    public async Task<Tuple<WrappedStream,string>?> GetImageStreamAndId(Jellyfin.Sdk.Generated.Models.BaseItemDto dto)
    {

        if (dto.ImageTags == null &&  dto.AlbumPrimaryImageTag == null)
            return null;

        var trackArt = dto.ImageTags.AdditionalData.TryGetValue("Primary", out var value) ;
        var desiredId = trackArt && value is string id ? id : dto.AlbumPrimaryImageTag;
        try
        {
            var ri =  apiClient.Items[dto.Id.Value].Images[ImageType.Primary.ToString()].ToGetRequestInformation();
            var uri = apiClient.BuildUri(ri);
            var r = new HttpRequestMessage(HttpMethod.Get,uri);
            r.Headers.Add("X-Emby-Token",settings.AccessToken);
            return new (new WrappedHttpStream(r), desiredId);
        }
        catch
        {
            return null;
        }
        return null;

    }

    public async Task<bool> LogIn(Gui gui)
    {
       authwindow = new AuthInfoWindow(this);
        await authwindow.ShowDialog(gui);
        return authwindow.Success;
    }

    public async Task<bool> GetServerUrl(Gui gui)
    {
       serverwindow = new ServerUrlWindow(this);
        await serverwindow.ShowDialog(gui);
        return serverwindow.Success;
    }

    public async Task<IList<LyricPhrase>?> GetLyrics(BaseItemDto songDto)
    {
        try
        {
            var ri =  await apiClient.Audio[songDto.Id.Value].Lyrics.GetAsync();
            var lyrics =  ri.Lyrics.Select(x => new LyricPhrase(((double)x.Start / 10000)!, x.Text+"\n")).ToList();
            return lyrics;
        }
        catch (Exception e)
        {
            return null;
        }
    }
}



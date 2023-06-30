using System.Diagnostics;
using System.Text;
using Jellyfin.Sdk;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
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
    private IPublicClientApplication app;
    public JellyFinHelper()
    {
        settings = new SdkClientSettings();
        settings.ClientVersion = typeof(JellyFinHelper).Assembly.GetName().Version.ToString();
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
        app = PublicClientApplicationBuilder.Create(SilverAudioPlayerJellyfinGuid).Build();
        Task.Run(async ()=> await MoreLibSecretStuff()).GetAwaiter().GetResult();
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
                    settings.BaseUrl = logininfo.url;
                    settings.AccessToken = logininfo.accessToken;
                    userDto = await userClient.GetUserByIdAsync(Guid.Parse(logininfo.userId));
                    validUser = true;
                }
               
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
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
            var logininfo = System.Text.Json.JsonSerializer.Serialize<LoginInfo>(new LoginInfo(settings.BaseUrl, authenticationResult.AccessToken, userDto.Id.ToString()));
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
      // var goofyah = audioClient.GetUniversalAudioStreamUrl(dto.Id, deviceId: "1", userId: userDto.Id);
      // Debug.WriteLine(goofyah);
        
        Uri baseUri = new Uri(settings.BaseUrl);
        Uri myUri = new Uri(baseUri, $"/Items/{dto.Id}/Download?api_key={settings.AccessToken}");
        return new WrappedSusHttpStream(() =>
        {
            var request = new HttpRequestMessage() {
                RequestUri =  myUri,
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
            var request = new HttpRequestMessage() {
                RequestUri = new Uri(fr),
                Method = HttpMethod.Get,
            };
            request.Headers.Add("X-Emby-Token",settings.AccessToken);
           var resp= await HttpClient.Client.SendAsync(request);
           if (resp.IsSuccessStatusCode)
           {
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
        return serverwindow.Success;
    }

    public async Task<bool> GetServerUrl(Gui gui)
    {
       serverwindow = new ServerUrlWindow(this);
        await serverwindow.ShowDialog(gui);
        return serverwindow.Success;
    }
}



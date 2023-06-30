using System.Diagnostics;
using System.Text.Json;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using SilverAudioPlayer.Shared;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;

namespace SilverAudioPlayer.DiscordRP;

public class RememberRichPresenceUrLsUsingImgurAndAJsonFile : IRememberRichPresenceURLs
{
    private MscArtFile[] cached;
    private readonly ApiClient client = new("d169c9264561822", "ce1616d067cb1493bc1df67b53e03660c5c02cc2");
    private readonly ImageEndpoint imageEndpoint;
    public bool Uploadit;

    public RememberRichPresenceUrLsUsingImgurAndAJsonFile()
    {
        imageEndpoint = new ImageEndpoint(client, HttpClient.Client);
    }

    public string? GetURL(Song? track, IMusicStatusInterfaceListener Env)
    {
        Debug.WriteLine("Geturl");
        GetCache();
        Debug.WriteLine("uplodit " + Uploadit);
        var a = Env.GetBestRepresentation(track?.Metadata?.Pictures);
        if (a == null) return null;
        if (cached.Any(x => x.hsh == a.Hash))
            return cached.First(x => x.hsh == a.Hash).url.Replace("https", "http");
        if (!Uploadit) return null;
        var res = Upload(a.Data);
        res = res.Replace("https", "http");
        cached = new List<MscArtFile>(cached) { new(a.Hash, res) }.ToArray();
        SetCache();
        return res;

    }

    public virtual string? Upload(WrappedStream bits)
    {
        var stream = bits.GetStream();
        try
        {
            var imageUpload = imageEndpoint.UploadImageAsync(stream);
            var res = imageUpload.GetAwaiter().GetResult();
            return res.Link;
        }
        finally
        {
            if (bits.ShouldDisposeStream)
            {
                stream.Dispose();
            }
        }
    }


    private void GetCache()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory,"Configs", "musicart.json")))
            cached = JsonSerializer.Deserialize<MscArtFile[]>(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Configs", "musicart.json")));
        cached ??= Array.Empty<MscArtFile>();
    }

    private void SetCache()
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Configs", "musicart.json"), JsonSerializer.Serialize(cached));
    }
}
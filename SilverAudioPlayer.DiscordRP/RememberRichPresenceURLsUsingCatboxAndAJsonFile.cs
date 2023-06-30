using System.Text.Json;
using CatBox.NET.Client;
using CatBox.NET.Requests;
using SilverAudioPlayer.Shared;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;

namespace SilverAudioPlayer.DiscordRP;

public class RememberRichPresenceURLsUsingCatboxAndAJsonFile : IRememberRichPresenceURLs
{
    private MscArtFile[] cached;
    public bool Uploadit;
    private CatBoxClient client;
    public RememberRichPresenceURLsUsingCatboxAndAJsonFile()
    {
        var options = new RealOptions()
        {
            Value = new()
            {
                CatBoxUrl = new Uri("https://catbox.moe/user/api.php")
            }
        };
        client = new CatBoxClient(HttpClient.Client,  options);
    }

    public string? GetURL(Song? track, IMusicStatusInterfaceListener Env)
    {
        GetCache();
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
            var res =  Task.Run(async () => await client.UploadImage(new StreamUploadRequest()
                { Stream = stream, FileName = "a" + bits.MimeType.FileExtensions[0] })).GetAwaiter().GetResult();
            return res;
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
using System.Diagnostics;
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
        client = new CatBoxClient(HttpClient.Client, options);
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
        if (res == null) return null;
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
            var res = Task.Run(async () => await client.UploadImage(new StreamUploadRequest()
                { Stream = stream, FileName = "a" + bits.MimeType.FileExtensions[0] })).GetAwaiter().GetResult();
            return res;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return null;
        }
        finally
        {
            stream.Dispose();
        }
    }


    private void GetCache()
    {
        if (File.Exists(Path.Combine(ConfigPath.BasePath, "musicart.json")))
            cached = JsonSerializer.Deserialize<MscArtFile[]>(
                File.ReadAllText(MusicArtFile));
        cached ??= Array.Empty<MscArtFile>();
    }

    private string MusicArtFile = Path.Combine(ConfigPath.BasePath, "musicart.json");
    object filelock = new();
    private void SetCache()
    {
        lock (filelock)
        {
            using var fs = new FileStream(MusicArtFile, FileMode.Create, FileAccess.Write);
            using var w = new StreamWriter(fs);
            w.Write(JsonSerializer.Serialize(cached));
            w.Flush();
        }
    }
}
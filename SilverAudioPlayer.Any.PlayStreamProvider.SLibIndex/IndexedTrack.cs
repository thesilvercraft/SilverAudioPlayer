using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ATL;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SLibIndex;

public class IndexedTrack : IMetadata
{
    public static readonly string ExpectedLocationDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLibIndex");
    public static string ExpectedFileLocation => Path.Combine(ExpectedLocationDir,CurrentFileVersionName+ BrotliSuffix);
    public const string CurrentFileVersionName = "tracksv3.json";
    public const string BrotliSuffix = ".br";
    public Guid GUID {get; set;}
    
    public string Location { get;  init;}
    public string Title {get;  init;}
    public string Artist {get;  init;}
    public string Album { get;  init; }
    public string Genre {get;  init;}
    public int? Year {get; init;}
    public ulong? Bitrate { get; init; }
    public ulong? SampleRate { get; init; }
    public uint? Channels { get; init; }
    public int? TrackNumber { get;  init;}
    public int? DiscNumber { get; init; }
    public string[]? Comments { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Entries { get;  init;} = new Dictionary<string, string>();
    public double? Duration { get;  init;}
    public string? Lyrics { get;  init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    
    public IList<LyricPhrase>? SyncedLyrics { get; set; }= null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    public IReadOnlyList<IPicture>? Pictures { get; init; } = null;
    public string? Art {get; set;}
    public static void SetIdFromMetadata(IndexedTrack track)
    {
        var a = track?.Title?.GetHashCode()??-1;
        var b = track?.Artist?.GetHashCode()??-1;
        var c = track?.Album?.GetHashCode()??-1;
        var d = track?.Genre?.GetHashCode()??-1;
        List<byte> by =
        [
            .. BitConverter.GetBytes(a),
            .. BitConverter.GetBytes(b),
            .. BitConverter.GetBytes(c),
            .. BitConverter.GetBytes(d),
        ];
        track.GUID = new Guid(by.ToArray());
    }
    public static List<IndexedTrack> LoadFromExpectedLocation(JsonSerializerOptions? options = default)
    {
        return LoadFromFile(ExpectedFileLocation,options);
    }
    public static List<IndexedTrack> LoadFromFile(string path,JsonSerializerOptions? options = default)
    {
        bool json = path.EndsWith(".json"), brotli = path.EndsWith(".br");

        if (!json && !brotli)
            throw new InvalidDataException("File path does not indicate brotli or json");
        using var fs = new FileStream(path, FileMode.Open);
        return LoadFromStream(fs, brotli,options);
    }

    public static List<IndexedTrack> LoadFromStream(Stream stream, bool brotli,JsonSerializerOptions? options = default)
    {
        if (!brotli)
        {
            return JsonSerializer.Deserialize<List<IndexedTrack>>(stream,options)!;
        }
        using BrotliStream bstream = new(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<List<IndexedTrack>>(bstream,options)!;
    }
    public void Dispose()
    {
    }
}

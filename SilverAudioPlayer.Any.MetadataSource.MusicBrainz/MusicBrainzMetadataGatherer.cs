using System.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverConfig;

[Export(typeof(IAdditionalMetadataGatherer))]
public class MusicBrainzMetadataGatherer : IAdditionalMetadataGatherer
{
    private string configpath = "/tmp/mb.json";
    public MusicBrainzMetadataGatherer(IConfigPathProvider configPathProvider)
    {
        configpath = configPathProvider.GetPath("mb.json");
        if (File.Exists(configpath))
        {
            ConfigData = JsonSerializer.Deserialize<Data>(File.ReadAllText(configpath));
        }
    }
    public string Name =>  "MusicBrainz metadata gatherer";

    public string Description => "";

    public WrappedStream? Icon => null;

    public Version? Version =>null;

    public string Licenses => "The MusicBrainzMetadataGatherer is licensed under the GPL3.0";

    public List<Tuple<Uri, URLType>>? Links => null;
    public object l = new();
    private Data ConfigData = new();
    public IMetadata? GatherAdditionalMetadata(IMetadataCombo original)
    {
        Guid id = Song.GetIdFromMetadata(original);
        if (ConfigData.Recordings.TryGetValue(id, out var rec))
        {
            return rec;
        }
        if(ConfigData.AlbumCache.TryGetValue(original.Album, out var album))
        {
            var r = album.Select(x => (x,LevenshteinDistance.Compute(x.Title.ToLowerInvariant(), original.Title.ToLowerInvariant()))).Where(x=>x.Item2<4).Select(x=>x.x).FirstOrDefault();
          if (r != null)
          {
              ConfigData.Recordings[id] = r;
              return rec;
          }
        }
        if (ConfigData.DontSearch.Contains(id)) return null;
        lock (l)
        {
                  try
                  {
                        var q = new Query("SilverAudioPlayer", "7.0", "https://gitlab.com/silvercraft/SilverAudioPlayer");
                        if (string.IsNullOrEmpty(original.Title)) return null;
                        string query;
                        query = string.IsNullOrEmpty(original.Album) ? $"\"{original.Title}\" AND qdur:{(int)(original.Duration / 2000)}" : $"\"{original.Title}\" AND release:\"{original.Album}\" AND qdur:{(int)(original.Duration / 2000)}";
                        /*if (!string.IsNullOrEmpty(original.Artist))
                        {
                            query += $" AND creditname:\"{original.Artist}\""; //meh
                        }*/
                        Debug.WriteLine($"Searching {query}");
                        var recordings =
                            q.FindRecordings(query, 1);
                        if (recordings.Results.Count <= 0) return null;
                        var originalres = recordings.Results.OrderByDescending(x => x.Score).First().Item;
                        ConfigData.Recordings[id] = new MusicBrainzMetadata(originalres, original.Album);
//                        Debug.WriteLine(ConfigData.Recordings[id].Releases[0].Media.Count);
                      var a = GetDesiredRelease(originalres.Releases,original.Album);
                      var albumId=originalres.Releases[a].Id;
                      if (!ConfigData.AlbumDownloaded.Contains(albumId))
                      {
                          var releases= q.LookupRelease(albumId, Include.Media| Include.Recordings | Include.Aliases | Include.ArtistCredits );
                          if (ConfigData.AlbumCache.TryGetValue(original.Album, out List<MusicBrainzMetadata>? value))
                          {
                              value.AddRange(releases.Media.SelectMany(x => x.Tracks).Select(x => new MusicBrainzMetadata(x.Recording, original.Album) ));
                          }
                          else
                          {
                              ConfigData.AlbumCache[ original.Album] =
                                  new List<MusicBrainzMetadata>( releases.Media.SelectMany(x => x.Tracks).Select(x => new MusicBrainzMetadata(x.Recording, original.Album)));
                          }
                          ConfigData.AlbumCache[original.Album]=   ConfigData.AlbumCache[original.Album].Distinct().ToList();
                          ConfigData.AlbumDownloaded.Add(albumId);
                      }
                       
                      File.WriteAllText(configpath, JsonSerializer.Serialize(ConfigData));
                      return ConfigData.Recordings[id];
                  }
                  catch (Exception e)
                  {
                      Debug.WriteLine(e);
                  }
                  ConfigData.DontSearch.Add(id);
                  return null;
        }
    }

    int GetDesiredRelease(IEnumerable<IRelease> releases, string name)
    {
        var s=releases.Select((e,i)=> new Tuple<IRelease, int>(e, i)).OrderBy(x => LevenshteinDistance.Compute(x.Item1?.Title?.ToLowerInvariant()??"", name.ToLowerInvariant())).FirstOrDefault();
        return s?.Item2 ?? 0;
    }
}
public class MusicBrainzMetadata : IMetadata
{
    public MusicBrainzMetadata(IRecording recording, string AlbumTitle)
    {
        MusicBrainzId = recording.Id;
        Title = recording.Title;
        if (recording.ArtistCredit != null)
        {
            Artist = string.Join(';', recording.ArtistCredit?.Select(x => x.Name));
        }
        Album = AlbumTitle;
        Genre=recording.Genres == null ? null : string.Join(';',recording.Genres?.Select(x=>x.Name));
        Year = recording.FirstReleaseDate.Year;
        Duration = recording.Length.Value.TotalMilliseconds;
        var jsonOptions = new JsonSerializerOptions() 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        Entries["OriginalMBData"] = JsonSerializer.Serialize(recording, jsonOptions);
    }

    public void Dispose()
    {
    }
    public Guid MusicBrainzId { get; set; }
    public string? Title { get;  }
    public string? Artist { get; }
    public string? Album { get; }
    public string? Genre { get; }
    public int? Year { get; }
    public ulong? Bitrate { get; }
    public ulong? SampleRate { get; }
    public uint? Channels { get; }
    public int? TrackNumber { get; }
    public int? DiscNumber { get; }
    public string[]? Comments { get; }
    public IDictionary<string, string>? Entries { get; } = new Dictionary<string, string>();
    public double? Duration { get; }
    public string? Lyrics { get; }
    public IList<LyricPhrase>? SyncedLyrics { get; } = null;
    public IReadOnlyList<IPicture>? Pictures { get; }= null;
}
public class Data
{
    public List<Guid> DontSearch { get; set; } = [];
    public SerializableDictionary<Guid, MusicBrainzMetadata> Recordings { get; set; } = [];
    public Dictionary<string, List<MusicBrainzMetadata>> AlbumCache { get; set; } = [];
    public List<Guid> AlbumDownloaded { get; set; } = [];

}


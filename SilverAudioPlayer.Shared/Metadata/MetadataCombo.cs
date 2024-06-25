namespace SilverAudioPlayer.Shared.Metadata;

public interface IMetadataCombo : IMetadata
{
    List<IMetadata> OriginalMetadata { get; }
}

public class MetadataCombo : IMetadataCombo
{
    public MetadataCombo()
    {
        OriginalMetadata = new();
    }

    public List<IMetadata> OriginalMetadata { get; private set; }
    public MetadataCombo(IReadOnlyCollection<IMetadata> metadata, IEnumerable<IAdditionalMetadataGatherer> metadataGatherers)
    {
        OriginalMetadata = metadata.OrderBy(x=>x.GetType().Name.Contains("FileMetadata")).ToList();
        OriginalMetadata.AddRange(metadataGatherers.Select(x => x.GatherAdditionalMetadata(this))
                .Where(x => x is not null).Cast<IMetadata>());
        SyncedLyrics = OriginalMetadata.Select(x => x.SyncedLyrics).Where(x=>x is not null).MaxBy(x => x!.Count);
        if ((SyncedLyrics is null or { Count: 0 }) && !string.IsNullOrEmpty(Lyrics) && (Lyrics[0] == '['))
        {
            //There is a chance that the unsynced lyrics are actually synced, lets try and read them
            try
            {
                SyncedLyrics = LrcParser.Parse(Lyrics);
            }
            catch (Exception e)
            {
                Logger.GetLogger(GetType())?.Error(e,"Error while trying to parse unsynced lyrics as lyrics");
            }
        }
        DiscNumber = metadata.Select(x => x.DiscNumber).MaxBy(x => x is not null);
    }
    public string? Title => OriginalMetadata.Select(x => x.Title).FirstOrDefault(x=>x!=null); 
    public string? Artist =>  OriginalMetadata.Select(x => x.Artist).FirstOrDefault(x=>x!=null);
    public string? Album => OriginalMetadata.Select(x => x.Album).FirstOrDefault(x=>x!=null);
    public string? Genre => OriginalMetadata.Select(x => x.Genre).FirstOrDefault(x=>x!=null);
    public int? Year => (int?)OriginalMetadata.Select(x => x.Year).Where(x => x is not 9999  or null).Average();
    public ulong? Bitrate => OriginalMetadata.Select(x => x.Bitrate).MaxBy(x => x is not null);
    public ulong? SampleRate => OriginalMetadata.Select(x => x.SampleRate).MaxBy(x => x is not null);
    public uint? Channels => OriginalMetadata.Select(x => x.Channels).MaxBy(x => x is not null);
    public int? TrackNumber => OriginalMetadata.Select(x => x.TrackNumber).MaxBy(x => x is not null);
    public int? DiscNumber { get; }

    public string[]? Comments => OriginalMetadata.Where(x => x.Comments is not null).SelectMany(x => x.Comments!)
        .Distinct().ToArray();
    public double? Duration => OriginalMetadata.Select(x => x.Duration).MaxBy(x => x is not null);
    public string? Lyrics => OriginalMetadata.Select(x => x.Lyrics).Where(x => x != null).MaxBy(x => !string.IsNullOrEmpty(x));
    public IList<LyricPhrase>? SyncedLyrics { get; }
    public IReadOnlyList<IPicture>? Pictures => OriginalMetadata.Select(x => x.Pictures).MaxBy(x => x?.Count);

    public IDictionary<string, string>? Enteries => OriginalMetadata.Select(x=>x.Enteries).MaxBy(x=>x?.Count);

    public void Dispose()
    {
        var metadata = OriginalMetadata.ToList();
        while (metadata.Count > 0)
        {
            metadata[0].Dispose();
            metadata.RemoveAt(0);
        }
        GC.SuppressFinalize(this);
    }

    
}
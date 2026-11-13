using Serilog;

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

    public MetadataCombo(IReadOnlyCollection<IMetadata> metadata,
        IEnumerable<IAdditionalMetadataGatherer> metadataGatherers)
    {
        OriginalMetadata = metadata.OrderBy(x => x.GetType().Name.Contains("FileMetadata")).ToList();
        OriginalMetadata.AddRange(metadataGatherers.Select(x => x.GatherAdditionalMetadata(this))
            .Where(x => x is not null).Cast<IMetadata>());
        SyncedLyrics = OriginalMetadata.Select(x => x.SyncedLyrics).Where(x => x is not null).MaxBy(x => x!.Count);
        if ((SyncedLyrics is null or { Count: 0 }) && !string.IsNullOrEmpty(Lyrics) && (Lyrics[0] == '['))
        {
            //There is a chance that the unsynced lyrics are actually synced, lets try and read them
            try
            {
                SyncedLyrics = LrcParser.Parse(Lyrics);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to parse unsynced lyrics as lyrics");
            }
        }

        DiscNumber = OriginalMetadata.Select(x => x.DiscNumber).FirstOrDefault(x => x is not null && x > 0);
    }

    public string? Title => OriginalMetadata.Select(x => x.Title).FirstOrDefault(x => x != null);
    public string? Artist => OriginalMetadata.Select(x => x.Artist).FirstOrDefault(x => x != null);
    public string? Album => OriginalMetadata.Select(x => x.Album).FirstOrDefault(x => x != null);
    public string? Genre => OriginalMetadata.Select(x => x.Genre).FirstOrDefault(x => x != null);
    public int? Year => OriginalMetadata.Select(x => x.Year).FirstOrDefault(x => x is not null and not 9999 and > 0);
    public ulong? Bitrate => OriginalMetadata.Select(x => x.Bitrate).Where(x => x != null).Max();
    public ulong? SampleRate => OriginalMetadata.Select(x => x.SampleRate).Where(x => x != null).Max();
    public uint? Channels => OriginalMetadata.Select(x => x.Channels).Where(x => x != null).Max();
    public int? TrackNumber => OriginalMetadata.Select(x => x.TrackNumber).FirstOrDefault(x => x is not null && x > 0);
    public int? DiscNumber { get; }

    public string[]? Comments => OriginalMetadata.Where(x => x.Comments is not null).SelectMany(x => x.Comments!)
        .Distinct().ToArray();

    public double? Duration => OriginalMetadata.Select(x => x.Duration).Where(x => x != null).Max();
    public string? Lyrics => OriginalMetadata.Select(x => x.Lyrics).FirstOrDefault(x => !string.IsNullOrEmpty(x));
    public IList<LyricPhrase>? SyncedLyrics { get; }
    public IReadOnlyList<IPicture>? Pictures => OriginalMetadata.Select(x => x.Pictures).MaxBy(x => x?.Count ?? 0);

    public IDictionary<string, string>? Entries => OriginalMetadata.Select(x => x.Entries).MaxBy(x => x?.Count ?? 0);

    public void Dispose()
    {
        while (OriginalMetadata.Count > 0)
        {
            OriginalMetadata[0].Dispose();
            OriginalMetadata.RemoveAt(0);
        }

        GC.SuppressFinalize(this);
    }
}
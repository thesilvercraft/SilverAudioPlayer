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
    public MetadataCombo(IReadOnlyCollection<IMetadata> metadata)
    {
        OriginalMetadata = metadata.OrderBy(x=>x.GetType().Name.Contains("FileMetadata")).ToList();
        Title = OriginalMetadata.Select(x => x.Title).FirstOrDefault();
        Artist = OriginalMetadata.Select(x => x.Artist).FirstOrDefault();
        Album = OriginalMetadata.Select(x => x.Album).FirstOrDefault();
        Genre = OriginalMetadata.Select(x => x.Genre).FirstOrDefault();
        Year = ((int?)OriginalMetadata.Select(x => x.Year).Where(x => x is not 9999  or null).Average());
        TrackNumber = OriginalMetadata.Select(x => x.TrackNumber).MaxBy(x => x is not null);
        Duration = OriginalMetadata.Select(x => x.Duration).MaxBy(x => x is not null);
        Bitrate = OriginalMetadata.Select(x => x.Bitrate).MaxBy(x => x is not null);
        SampleRate = OriginalMetadata.Select(x => x.SampleRate).MaxBy(x => x is not null);
        Channels = OriginalMetadata.Select(x => x.Channels).MaxBy(x => x is not null);
        Comments = OriginalMetadata.Where(x=>x.Comments is not null).SelectMany(x => x.Comments!).Distinct().ToArray();
        Lyrics = OriginalMetadata.Select(x => x.Lyrics).Where(x => x != null).MaxBy(x => !string.IsNullOrEmpty(x));
        SyncedLyrics = OriginalMetadata.Select(x => x.SyncedLyrics).Where(x=>x is not null).MaxBy(x => x!.Count);
        if ((SyncedLyrics is null or { Count: 0 }) && !string.IsNullOrEmpty(Lyrics) && (Lyrics[0] == '['))
        {
            //There is a chance that the unsynced lyrics are actually synced, lets try and read them
            try
            {
                var s = Opportunity.LrcParser.Lyrics.Parse(Lyrics);
                SyncedLyrics = s.Lyrics.Lines.Select(x =>
                        new LyricPhrase((int)(x.Timestamp.Ticks / TimeSpan.TicksPerMillisecond), x.Content + "\n"))
                    .ToList();
            }
            catch (Exception e)
            {
                Logger.GetLogger(GetType())?.Error(e,"Error while trying to parse unsynced lyrics as lyrics");
            }
        }
        DiscNumber = metadata.Select(x => x.DiscNumber).MaxBy(x => x is not null);
    }

    public string? Title { get; }
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
    public double? Duration { get; }
    public string? Lyrics { get; }
    public IList<LyricPhrase>? SyncedLyrics { get; }
    public IReadOnlyList<IPicture>? Pictures => OriginalMetadata.Select(x => x.Pictures).MaxBy(x => x?.Count);

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
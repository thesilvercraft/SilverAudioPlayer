using FuzzySharp.Extensions;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class MetadataCombo : IMetadata
{
    public MetadataCombo()
    {

    }

    public List<IMetadata> OriginalMetadatas { get; private set; }
    public MetadataCombo(IReadOnlyCollection<IMetadata> metadatas)
    {
        OriginalMetadatas = metadatas.OrderBy(x=>x.GetType().Name.Contains("FileMetadata")).ToList();
        Title =OriginalMetadatas.Select(x => x.Title).FirstOrDefault();
        Artist = OriginalMetadatas.Select(x => x.Artist).FirstOrDefault();
        Album = OriginalMetadatas.Select(x => x.Album).FirstOrDefault();
        Genre = OriginalMetadatas.Select(x => x.Genre).FirstOrDefault();
        Year = ((int?)OriginalMetadatas.Select(x => x.Year).Where(x => x is not 9999 or 0 or null).Average());
        TrackNumber = OriginalMetadatas.Select(x => x.TrackNumber).MaxBy(x => x is not null);
        Duration = OriginalMetadatas.Select(x => x.Duration).MaxBy(x => x is not null);
        Bitrate = OriginalMetadatas.Select(x => x.Bitrate).MaxBy(x => x is not null);
        SampleRate = OriginalMetadatas.Select(x => x.SampleRate).MaxBy(x => x is not null);
        Channels = OriginalMetadatas.Select(x => x.Channels).MaxBy(x => x is not null);
     
        Lyrics = OriginalMetadatas.Select(x => x.Lyrics).Where(x => x != null).MaxBy(x => !string.IsNullOrEmpty(x));
        SyncedLyrics = OriginalMetadatas.Select(x => x.SyncedLyrics).Where(x=>x!=null).MaxBy(x => x.Count);
        if ((SyncedLyrics is null or { Count: 0 }) && !string.IsNullOrEmpty(Lyrics) && (Lyrics[0] == '['))
        {
            //There is a chance that the unsynced lyrics are actually synced, lets try and read them

            try
            {
                var s = global::Opportunity.LrcParser.Lyrics.Parse(Lyrics);
                SyncedLyrics = s.Lyrics.Lines.Select(x => new LyricPhrase((int)(x.Timestamp.Ticks / TimeSpan.TicksPerMillisecond), x.Content + "\n")).ToList();
            }
            catch { }
        }
        DiscNumber = metadatas.Select(x => x.DiscNumber).MaxBy(x => x is not null);
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
    public IReadOnlyList<IPicture>? Pictures => OriginalMetadatas.Select(x => x.Pictures).MaxBy(x => x?.Count);

    public void Dispose()
    {
        var metadatas = OriginalMetadatas.ToList();
        while (metadatas.Count > 0)
        {
            metadatas[0].Dispose();
            metadatas.RemoveAt(0);
        }
        GC.SuppressFinalize(this);
    }

    
}
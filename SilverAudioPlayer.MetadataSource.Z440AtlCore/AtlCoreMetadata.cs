using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;


public class AtlCoreMetadata : IMetadata
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public int? Year { get; init; }
    public ulong? Bitrate { get; init; }
    public ulong? SampleRate { get; init; }
    public uint? Channels { get; init; }

    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string[]? Comments { get; init; }

    /// <summary>
    ///     duration in milliseconds
    /// </summary>
    public double? Duration { get; init; }

    public string? Lyrics { get; init; }
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<ATLCOREPicture> CachedPicture;
    public IReadOnlyList<IPicture>? Pictures
    {
        get
        {
            if (CachedPicture != null)
            {
                return CachedPicture;
            }
           return CachedPicture = OGInfo.EmbeddedPictures.Select(x => new ATLCOREPicture(x)).ToList();
        }
    }

    private Stream TrackStream;
    public AtlCoreMetadata(WrappedStream stream)
    {
        TrackStream = stream.GetStream();
        Track theTrack = new(TrackStream,stream.MimeType.RealMimeTypeToFakeMimeType());
        if (stream is not WrappedRegenerativeStream)
        {
            _ = Pictures?.Any(); // yes
        }
        OGInfo = theTrack;
        Title = theTrack.Title;
        Artist = theTrack.Artist;
        Album = theTrack.Album;
        Genre = theTrack.Genre;
        Year = theTrack.Year;
        TrackNumber = theTrack.TrackNumber;
        Duration = theTrack.DurationMs;
        Bitrate = (ulong?)theTrack.Bitrate;
        SampleRate = (ulong?)theTrack.SampleRate;
        Channels = (uint?)theTrack.ChannelsArrangement.NbChannels;
        Lyrics = theTrack.Lyrics.UnsynchronizedLyrics;
        SyncedLyrics = theTrack.Lyrics.SynchronizedLyrics.Select(x => new LyricPhrase(x.TimestampMs, x.Text)).ToList();
        DiscNumber = theTrack.DiscNumber;
    }

    public Track OGInfo { get; private set; }


    private void ReleaseUnmanagedResources()
    {
        OGInfo = null;
        CachedPicture = null;
    }
    

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            TrackStream.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~AtlCoreMetadata()
    {
        Dispose(false);
    }
}
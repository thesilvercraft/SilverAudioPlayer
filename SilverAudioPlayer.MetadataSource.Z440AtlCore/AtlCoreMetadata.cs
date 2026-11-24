using ATL;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public sealed class AtlCoreMetadata : IMetadata
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
    private List<ATLCOREPicture>? _cachedPicture;

    public IReadOnlyList<IPicture>? Pictures => _cachedPicture;

    public IDictionary<string, string>? Entries { get; init; }

    public AtlCoreMetadata(WrappedStream stream, bool stopLoadingPictures = false)
    {
        using var trackStream = stream.GetStream();
        var theTrack = new Track(trackStream, stream.MimeType.RealMimeTypeToFakeMimeType());

        Title = theTrack.Title;
        Artist = theTrack.Artist;
        Album = theTrack.Album;
        Genre = theTrack.Genre;
        Year = theTrack.Year;
        Bitrate = (ulong?)theTrack.Bitrate;
        SampleRate = (ulong?)theTrack.SampleRate;
        Channels = (uint?)theTrack.ChannelsArrangement.NbChannels;
        TrackNumber = theTrack.TrackNumber;
        DiscNumber = theTrack.DiscNumber;
        Duration = theTrack.DurationMs;
        Lyrics = theTrack.Lyrics
            .FirstOrDefault(x => x.Format == LyricsInfo.LyricsFormat.UNSYNCHRONIZED)
            ?.UnsynchronizedLyrics;
        Entries = theTrack.AdditionalFields != null
            ? new Dictionary<string, string>(theTrack.AdditionalFields)
            : null;

        if (!stopLoadingPictures)
        {
            _cachedPicture = theTrack.EmbeddedPictures?
                .Where(x => x?.PictureData is { Length: > 0 })
                .Select(x => new ATLCOREPicture(x))
                .ToList();
        }
        else
        {
            _cachedPicture = [];
        }

        foreach (var lyricsInfo in theTrack.Lyrics
                     .Where(ly => ly.Format.HasFlag(LyricsInfo.LyricsFormat.SYNCHRONIZED)))
        {
            SyncedLyrics = lyricsInfo.SynchronizedLyrics
                .Select(x => new LyricPhrase(x.TimestampStart, x.Text.EndsWith('\n') ? x.Text : x.Text + '\n'))
                .ToList();
        }
    }

    public void Dispose()
    {
        if (_cachedPicture != null)
        {
            foreach (var picture in _cachedPicture)
            {
                picture.Dispose();
            }

            _cachedPicture = null;
        }
    }
}
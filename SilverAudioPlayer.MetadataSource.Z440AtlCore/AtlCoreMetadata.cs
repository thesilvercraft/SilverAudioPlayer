#define hackymemory

using ATL;
#if hackymemory
using System.Reflection;
using ATL.AudioData;
#endif
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class AtlCoreMetadata : IMetadata
{
    public string? Title => theTrack.Title;
    public string? Artist => theTrack.Artist;
    public string? Album => theTrack.Album;
    public string? Genre => theTrack.Genre;
    public int? Year => theTrack.Year;
    public ulong? Bitrate => (ulong?)theTrack.Bitrate;
    public ulong? SampleRate => (ulong?)theTrack.SampleRate;
    public uint? Channels => (uint?)theTrack.ChannelsArrangement.NbChannels;

    public int? TrackNumber => theTrack.TrackNumber;
    public int? DiscNumber => theTrack.DiscNumber;
    public string[]? Comments { get; init; }

    /// <summary>
    ///     duration in milliseconds
    /// </summary>
    public double? Duration => theTrack.DurationMs;

    public string? Lyrics => theTrack.Lyrics.UnsynchronizedLyrics;
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<ATLCOREPicture>? _cachedPicture;

    public IReadOnlyList<IPicture>? Pictures =>
        _cachedPicture;

    private Track theTrack { get; set; }
    public IDictionary<string, string>? Enteries {get;init;}

#if hackymemory
    private static readonly PropertyInfo currentEmbeddedPictures = typeof(Track)
        .GetProperty("currentEmbeddedPictures", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo initialEmbeddedPictures = typeof(Track)
        .GetField("initialEmbeddedPictures", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo fileIO = typeof(Track)
        .GetField("fileIO", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo tagData = typeof(MetaDataHolder)
        .GetProperty("tagData", BindingFlags.NonPublic | BindingFlags.Instance);

#endif
    public AtlCoreMetadata(WrappedStream stream)
    {
        using var trackStream = stream.GetStream();
        theTrack = new Track(trackStream, stream.MimeType.RealMimeTypeToFakeMimeType());
        _cachedPicture = theTrack?.EmbeddedPictures?.Where(x => x != null).Select(x => new ATLCOREPicture(x))
            .ToList();
#if hackymemory
        (currentEmbeddedPictures.GetValue(theTrack) as ICollection<PictureInfo>).Clear();
        (initialEmbeddedPictures.GetValue(theTrack) as ICollection<PictureInfo>).Clear();
        currentEmbeddedPictures.SetValue(theTrack, null);
        initialEmbeddedPictures.SetValue(theTrack, null);
        var fio = fileIO.GetValue(theTrack);
        var fioType = fio.GetType();
        var metaFF = fioType?.GetField("metaData", BindingFlags.Instance | BindingFlags.NonPublic);
        if (metaFF != null)
        {
            var meta = metaFF.GetValue(fio) as IMetaDataIO;
            if (meta is MetaDataHolder metaDataHolder)
            {
                var tagddata = tagData.GetValue(metaDataHolder);
                if (tagddata is TagData td)
                {
                    td.Pictures.Clear();
                    td.Pictures = null;
                }
            }
        }
#endif
        Enteries = theTrack?.AdditionalFields;
        SyncedLyrics = theTrack.Lyrics.SynchronizedLyrics.Select(x => new LyricPhrase(x.TimestampMs, x.Text)).ToList();
    }


    private void ReleaseUnmanagedResources()
    {
        theTrack = null;
        _cachedPicture = null;
    }


    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
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
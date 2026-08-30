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

    public string? Lyrics => theTrack.Lyrics.FirstOrDefault(x=>x.Format== LyricsInfo.LyricsFormat.UNSYNCHRONIZED)?.UnsynchronizedLyrics; 
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<ATLCOREPicture>? _cachedPicture;

    public IReadOnlyList<IPicture>? Pictures =>
        _cachedPicture;

    private Track theTrack { get; set; }
    public IDictionary<string, string>? Entries {get;init;}

#if hackymemory
    private static readonly PropertyInfo currentEmbeddedPictures = typeof(Track)
        .GetProperty("currentEmbeddedPictures", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo initialEmbeddedPictures = typeof(Track)
        .GetField("initialEmbeddedPictures", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo fileIO = typeof(Track)
        .GetField("fileIO", BindingFlags.NonPublic | BindingFlags.Instance);


#endif
    public AtlCoreMetadata(WrappedStream stream, bool stopLoadingPictures=false)
    {
        using var trackStream = stream.GetStream();
        theTrack = new Track(trackStream, stream.MimeType.RealMimeTypeToFakeMimeType());
        if(!stopLoadingPictures)
        {
            _cachedPicture = theTrack?.EmbeddedPictures?.Where(x => x != null).Select(x => new ATLCOREPicture(x))
         .ToList();
        }
        else
        {
            _cachedPicture = [];
        }
     
#if hackymemory

        theTrack.EmbeddedPictures.Clear();
        (currentEmbeddedPictures.GetValue(theTrack) as ICollection<PictureInfo>).Clear();
        (initialEmbeddedPictures.GetValue(theTrack) as ICollection<PictureInfo>).Clear();
        currentEmbeddedPictures.SetValue(theTrack, new List<PictureInfo>());
        initialEmbeddedPictures.SetValue(theTrack, new List<PictureInfo>());
        var fio = fileIO.GetValue(theTrack);
        var fioType = fio.GetType();
        if (fio is IAudioDataIO)
        {
            var metaFF = fioType?.GetProperty("Metadata", BindingFlags.Instance | BindingFlags.Public);
            var metaX = metaFF.GetValue(fio);
            void ProcessTags(IMetaDataIO io)
            {
                if (io == null) return;
                io.EmbeddedPictures.Clear();
                if (io is MetaDataHolder metaDataHolder)
                {
                    metaDataHolder.EmbeddedPictures.Clear();
                    metaDataHolder.EmbeddedPictures = [];
                }
            }
            if(metaX is IMetaDataIO meta)
            {
                ProcessTags(meta);
            }
            var audioDataManagerF = fioType?.GetField("audioManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var m = audioDataManagerF.GetValue(fio) as AudioDataManager;
            if(m is { })
            {
                ProcessTags(m.ID3v1);
                ProcessTags(m.ID3v2);
                ProcessTags(m.APEtag);
                ProcessTags(m.NativeTag);
            }

        }

#endif
        Entries = theTrack?.AdditionalFields;
        foreach (var lyricsInfo in theTrack.Lyrics
                     .Where(ly=>ly.Format.HasFlag(LyricsInfo.LyricsFormat.SYNCHRONIZED)))
        {
            SyncedLyrics = lyricsInfo.SynchronizedLyrics.Select(x => new LyricPhrase(x.TimestampStart, x.Text.EndsWith('\n')?x.Text : x.Text+'\n')).ToList();
        }
    }


    private void ReleaseUnmanagedResources()
    {
        theTrack = null;
        _cachedPicture = null;
    }


    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
       
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
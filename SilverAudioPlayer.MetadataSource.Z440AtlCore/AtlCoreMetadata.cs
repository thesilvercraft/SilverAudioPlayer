using System.Diagnostics;
using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;
public class AtlCoreMetadata : IMetadata
{
    public string? Title => theTrack.Title;
    public string? Artist => theTrack.Artist;
    public string? Album =>theTrack.Album;
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

    public string? Lyrics =>theTrack.Lyrics.UnsynchronizedLyrics;
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<ATLCOREPicture>? _cachedPicture;
    public IReadOnlyList<IPicture>? Pictures =>
        _cachedPicture;

    private Track theTrack { get; set; }
    public AtlCoreMetadata(WrappedStream stream)
    {
        var memstart= System.GC.GetTotalMemory(true);
        
        using var trackStream = stream.GetStream();
         theTrack = new(trackStream,stream.MimeType.RealMimeTypeToFakeMimeType());
         _cachedPicture = theTrack?.EmbeddedPictures?.Where(x => x != null).Select(x => new ATLCOREPicture(x))
                .ToList();
        SyncedLyrics = theTrack.Lyrics.SynchronizedLyrics.Select(x => new LyricPhrase(x.TimestampMs, x.Text)).ToList();
        var memend= System.GC.GetTotalMemory(true);

       
            Debug.WriteLine("Size of metadata "+(memend-memstart));

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
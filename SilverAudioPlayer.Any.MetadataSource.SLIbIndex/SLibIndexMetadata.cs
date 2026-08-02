using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.SLIbIndex;

public class SLibIndexMetadata : IMetadata
{
    public void Dispose()
    {
    }

    public string? Title => null;

    public string? Artist => null;

    public string? Album => null;

    public string? Genre => null;

    public int? Year => null;

    public ulong? Bitrate => null;

    public ulong? SampleRate => null;

    public uint? Channels => null;

    public int? TrackNumber => null;

    public int? DiscNumber => null;

    public string[]? Comments => null;

    public IDictionary<string, string>? Entries => null;

    public double? Duration => null;

    public string? Lyrics  {
        get;
        set;
    }

    public IList<LyricPhrase>? SyncedLyrics
    {
        get;
        set;
    }

    public IReadOnlyList<IPicture>? Pictures => null;
}
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

public sealed class SLibIndexLiteTrack : IMetadata
{
    public Guid Guid { get; init; }
    public string? FileName { get; init; }
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
    public double? Duration { get; init; }
    public string? ArtHash { get; init; }
    public bool HasArt { get; init; }
    public bool HasLyrics { get; init; }
    public bool HasSyncedLyrics { get; init; }
    public string[]? Comments => null;
    public IDictionary<string, string>? Entries => null;
    public string? Lyrics { get; set; }
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    public IReadOnlyList<IPicture>? Pictures => null;

    public void Dispose()
    {
    }
}

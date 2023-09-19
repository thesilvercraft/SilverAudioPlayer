namespace SilverAudioPlayer.Shared;

public abstract class Metadata :IMetadata
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
    public IReadOnlyList<IPicture>? Pictures { get; init; }
    public abstract void Dispose();
}
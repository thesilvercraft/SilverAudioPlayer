namespace SilverAudioPlayer.Shared.Metadata;

public abstract class Metadata : IMetadata
{
    ///<inheritdoc/>
    public string? Title { get; init; }
    /// <inheritdoc />
    public string? Artist { get; init; }
    /// <inheritdoc />
    public string? Album { get; init; }
    /// <inheritdoc />
    public string? Genre { get; init; }
    /// <inheritdoc />
    public int? Year { get; init; }
    /// <inheritdoc />
    public ulong? Bitrate { get; init; }
    /// <inheritdoc />
    public ulong? SampleRate { get; init; }
    /// <inheritdoc />
    public uint? Channels { get; init; }
    /// <inheritdoc />
    public int? TrackNumber { get; init; }
    /// <inheritdoc />
    public int? DiscNumber { get; init; }
    /// <inheritdoc />
    public string[]? Comments { get; init; }
    /// <inheritdoc />
    public double? Duration { get; init; }
    /// <inheritdoc />
    public string? Lyrics { get; init; }
    /// <inheritdoc />
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    /// <inheritdoc />
    public IReadOnlyList<IPicture>? Pictures { get; init; }
    /// <inheritdoc />
    public IDictionary<string, string>? Enteries { get; init; }
    /// <inheritdoc />
    public abstract void Dispose();
}
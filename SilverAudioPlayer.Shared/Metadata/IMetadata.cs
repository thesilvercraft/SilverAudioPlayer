namespace SilverAudioPlayer.Shared.Metadata;

/// <summary>
/// Represents metadata associated with an audio track or media item.
/// </summary>
/// <remarks>
/// Implementations of this interface provide various details such as title, artist, album, and technical specifications.
/// </remarks>
public interface IMetadata : IDisposable
{
    /// <summary>
    /// The title of a song
    /// </summary>
    public string? Title { get; }
    /// <summary>
    /// The artist of a song
    /// </summary>
    public string? Artist { get; }
    /// <summary>
    /// The album of a song
    /// </summary>
    public string? Album { get; }
    /// <summary>
    /// The genre of a song
    /// </summary>
    public string? Genre { get; }
    /// <summary>
    /// The year a song was released
    /// </summary>
    public int? Year { get; }
    /// <summary>
    /// The bitrate of the file housing the song
    /// </summary>
    public ulong? Bitrate { get; }
    /// <summary>
    ///  The sample rate of the file housing the song
    /// </summary>
    public ulong? SampleRate { get; }
    /// <summary>
    ///  The amount of channels of the file housing the song
    /// </summary>
    public uint? Channels { get; }
    /// <summary>
    /// The track number of a song
    /// </summary>
    public int? TrackNumber { get; }
    /// <summary>
    /// The disc number of a song
    /// </summary>
    public int? DiscNumber { get; }
    /// <summary>
    /// Comments embedded in the metadata of a song
    /// </summary>
    public string[]? Comments { get; }
    /// <summary>
    /// Comments that are key value pairs
    /// </summary>
    public IDictionary<string, string>? Entries { get; }
    /// <summary>
    ///  Duration in milliseconds according to the metadata
    /// </summary>
    public double? Duration { get; }
    /// <summary>
    /// Plain text lyrics
    /// </summary>
    public string? Lyrics { get; }
    /// <summary>
    /// Synchronised lyrics embedded in the song's metadata
    /// </summary>
    public IList<LyricPhrase>? SyncedLyrics { get; }
    /// <summary>
    /// Pictures embedded in the song's metadata
    /// </summary>
    public IReadOnlyList<IPicture>? Pictures { get; }
}
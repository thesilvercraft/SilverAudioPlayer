using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;
/// <summary>
/// A provider for metadata
/// </summary>
public interface IMetadataProvider : ICodeInformation
{
    /// <summary>
    /// Get the metadata embedded in a stream
    /// </summary>
    /// <param name="stream">a stream to get the metadata from</param>
    /// <returns>metadata</returns>
    public Task<IMetadata?> GetMetadata(WrappedStream stream);
    /// <summary>
    /// Check if the given stream is supported by the metadata provider
    /// </summary>
    /// <param name="stream">a stream to check</param>
    /// <returns>a true or false</returns>
    public bool CanGetMetadata(WrappedStream stream);
}

public interface ITrackLoadInfo
{
    public string URL { get; set; }
    public string Source { get; }
}
public interface IPlaylist
{
    public IEnumerable<ITrackLoadInfo> Tracks { get; }
    public int? IndexOfActive { get; }
    public double? PositionOfActive { get; }
}

public interface IPlaylistReader : ICodeInformation
{
    public IReadOnlyList<MimeType> SupportedMimes { get; }
    public bool TryParse(WrappedStream stream, out IPlaylist? playlist);
}
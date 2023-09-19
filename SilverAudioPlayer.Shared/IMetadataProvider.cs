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
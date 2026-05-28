namespace SilverAudioPlayer.Shared.Metadata;

/// <summary>
/// Defines an interface for components responsible for gathering additional metadata.
/// </summary>
/// <remarks>
/// Implementations of this interface are intended to augment the original metadata
/// gathered from a source by fetching extra information (e.g., from online services)
/// and combining it into a richer set of <see cref="IMetadata"/>.
/// </remarks>
public interface IAdditionalMetadataGatherer : ICodeInformation
{
    /// <summary>
    /// Gathers additional metadata based on the provided original metadata combination.
    /// </summary>
    /// <param name="original">The combined metadata from various sources.</param>
    /// <returns>An optional instance of IMetadata containing the gathered additional metadata, or null if no further information is found.</returns>
    public IMetadata? GatherAdditionalMetadata(IMetadataCombo original);
}
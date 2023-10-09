using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     A interface that describes the User Interface
/// </summary>
public interface IPlayerEnvironment : ICodeInformation
{
    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream);
}
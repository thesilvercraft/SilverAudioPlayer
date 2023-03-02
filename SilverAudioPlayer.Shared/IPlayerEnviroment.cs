namespace SilverAudioPlayer.Shared;

/// <summary>
///     A interface that describes the User Interface
/// </summary>
public interface IPlayerEnviroment : ICodeInformation
{
    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream);
}
namespace SilverAudioPlayer.Shared;

/// <summary>
///     A interface that describes the User Interface
/// </summary>
public interface IPlayerEnviroment : ICodeInformation
{
    public Task<Metadata?>? GetMetadataAsync(WrappedStream stream);
}
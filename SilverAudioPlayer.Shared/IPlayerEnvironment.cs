using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     A interface that describes the User Interface
/// </summary>
public interface IPlayerEnvironment : ICodeInformation
{
    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream);
}

public interface IFullPlayerEnvironment : IPlayStreamProviderListener,
    ISyncEnvironmentListener, IMusicStatusInterfaceListenerAdmin, IConfigurableListener
{
    
}
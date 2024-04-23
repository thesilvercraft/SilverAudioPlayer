using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     A interface that describes the User Interface
/// </summary>
public interface IPlayerEnvironment : ICodeInformation
{
    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream);
    public Task<IReadOnlyList<MimeType>> SupportedMimeTypes();
    public IDictionary<string,string>? GetEnteriesForCurrentTrack();
}

public interface IFullPlayerEnvironment : IPlayStreamProviderListener,
    ISyncEnvironmentListener, IMusicStatusInterfaceListenerAdmin, IConfigurableListener
{
    
}
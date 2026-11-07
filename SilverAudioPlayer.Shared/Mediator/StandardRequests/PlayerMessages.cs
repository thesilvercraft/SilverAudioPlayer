using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Shared.StandardRequests;

// Application messages used by the core, UI, and plugin environment adapters.
public sealed record TrackChangedMessage(Song? Song) : IMessage;

public sealed record PlaybackStateChangedMessage(PlaybackState State) : IMessage;

public sealed record PlaybackPositionChangedMessage(TimeSpan Position) : IMessage;

public sealed record OpenMusicStatusInterfacesMessage(
    IMusicStatusInterfaceListenerAdmin Listener,
    IEnumerable<IMusicStatusInterface>? Interfaces,
    Func<string, bool> IsPluginAllowed) : IMessage;

public sealed record AddMusicStatusInterfaceMessage(
    IMusicStatusInterface StatusInterface,
    IMusicStatusInterfaceListenerAdmin Listener) : IMessage;

public sealed record RemoveMusicStatusInterfaceMessage(
    IMusicStatusInterface StatusInterface,
    IMusicStatusInterfaceListener Listener) : IMessage;

public sealed record ResetUiScrollBarMessage : IMessage;

public sealed record SetScrollBarTextMessage(TimeSpan Duration) : IMessage;

public sealed record RefreshMetadataAndScrollBarMessage : IMessage;

public sealed record ShowPlayerMessageBoxMessage(string Title, string Message) : IMessage;

public sealed record PostToUiMessage(Action Action) : IMessage;

public sealed record LoadSongMessage(WrappedStream Stream, IMetadata? AdditionalMetadata = null) : IMessage;

public sealed record LoadSongsMessage(IEnumerable<WrappedStream> Streams) : IMessage;

public sealed record LoadSongsWithMetadataMessage(IEnumerable<Tuple<WrappedStream, IMetadata?>> Streams) : IMessage;

public sealed record ProcessFilesMessage(IEnumerable<string> Files) : IMessage;

public sealed record FilterFilesQuery(IEnumerable<string> Files) : IQuery<IEnumerable<string>>;

public sealed record GetQueueQuery : IQuery<List<Song>>;
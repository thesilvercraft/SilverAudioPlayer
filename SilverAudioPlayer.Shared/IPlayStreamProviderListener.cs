using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Shared;

public interface IPlayStreamProviderListenerBare
{
    /// <summary>
    /// Loads a wrappedstream into the queue
    /// </summary>
    /// <param name="s">wrappedstream to load</param>
    void LoadSong(WrappedStream s);
    /// <summary>
    /// Loads a wrappedstream into the queue and provide additional metadata
    /// </summary>
    /// <param name="s">wrappedstream to load</param>
    /// <param name="additionalMetadata">additional metadata</param>

    void LoadSong(WrappedStream s, IMetadata additionalMetadata);

    /// <summary>
    /// Loads URLS into the queue
    /// </summary>
    /// <param name="files">URLs to load</param>
    public void ProcessFiles(IEnumerable<string> files);

    /// <summary>
    /// Load <see cref="WrappedStream"/>s
    /// </summary>
    /// <param name="streams">Streams to load</param>
    void LoadSongs(IEnumerable<WrappedStream> streams);
    /// <summary>
    /// Load <see cref="WrappedStream"/>s with additional metadata
    /// </summary>
    /// <param name="streams">Streams to load</param>
    void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams);

    IEnumerable<string> FilterFiles(IEnumerable<string> files);
}
public interface IPlayStreamProviderListener : IPlayerEnvironment, IPlayStreamProviderListenerBare;
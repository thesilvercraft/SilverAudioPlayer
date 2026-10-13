using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Core;
/// <summary>
/// Provides some of the main methods used by a Logic object, the backbone for an implementation of SAP
/// </summary>
public interface ILogic : IPlayStreamProviderListenerBare
{
    /// <summary>
    /// Subscribes a <see cref="IMusicStatusInterface"/> to the current logic
    /// </summary>
    /// <param name="statusInterface">the interface to subscribe</param>
    /// <param name="logicMusicInterfaceListener">the logic's music interface listener</param>
    void AddMSI(IMusicStatusInterface statusInterface, IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener);

    /// <summary>
    /// Unsubscribes a <see cref="IMusicStatusInterface"/> to the current logic
    /// </summary>
    /// <param name="statusInterface">the interface to unsubscribe</param>
    /// <param name="logicMusicInterfaceListener">the logic's music interface listener</param>
    void RemoveMsi(IMusicStatusInterface statusInterface, IMusicStatusInterfaceListener logicMusicInterfaceListener);
    /// <summary>
    /// Play or Pause depending on the current playback status
    /// </summary>
    /// <param name="allowStart">If the player is stopped, is starting the playback of a new song allowed</param>
    void PlayPause(bool allowStart);
    /// <summary>
    /// Skip to the next track (i.e. stop playing, play next track)
    /// </summary>
    void Next();
    /// <summary>
    /// Load the previous track (i.e. stop playing, play previous track)
    /// </summary>
    void Previous();
    /// <summary>
    /// Plays the current track or starts playing the next
    /// </summary>
    void Play();
    /// <summary>
    /// Pauses the current track
    /// </summary>
    void Pause();
    /// <summary>
    /// Starts playing or loads the current track
    /// </summary>
    /// <param name="play">automatically start playing after load</param>
    /// <param name="resetsal">reset the StopAutoLoading value</param>
    void StartPlaying(bool play = true, bool resetsal = false);
    /// <summary>
    /// Unloads the current track (i.e. stops playing, removes it)
    /// </summary>
    void RemoveTrack();


    /// <summary>
    /// Clears the queue
    /// </summary>
    void ClearAll();
    /// <summary>
    /// Adds a song
    /// </summary>
    /// <param name="song">the song to load</param>
    /// <param name="expectmore">are other songs going to be added (should it not sort)</param>
    void AddSong(Song song, bool expectmore = false);
    /// <summary>
    /// Resolves the player associated with the file type of a stream, if found it returns it if not it returns null.
    /// </summary>
    /// <param name="stream">the stream to load</param>
    /// <returns>a player</returns>
    Task<IPlay?> GetPlayerFromStream(WrappedStream stream);
    /// <summary>
    /// Gets the related metadata from a stream
    /// </summary>
    /// <param name="stream">the stream</param>
    /// <param name="additionalMetadata">additional already known metadata to include in metadatacombo</param>
    /// <returns>A <see cref="MetadataCombo"/> containing all the known metadata</returns>
    Task<IMetadata?>? GetMetadataFromStream(WrappedStream stream, IEnumerable<IMetadata>? additionalMetadata = null);

    /// <summary>
    /// Copy the queue to a list of songs
    /// </summary>
    /// <returns>a list of songs</returns>
    List<Song> GetQueueCopy();
}
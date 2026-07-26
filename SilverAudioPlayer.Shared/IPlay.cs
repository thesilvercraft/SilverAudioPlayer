using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public interface IPlayStreams : IPlay
{
    /// <summary>
    ///     Loads a new stream to be played using the player
    /// </summary>
    /// <param name="stream">The stream to be loaded</param>
    void LoadStream(WrappedStream stream);
    public IReadOnlyList<MimeType>? SupportedMimes { get; }
}
[Obsolete("SilverAudioPlayer doesn't make use of this yet")]
public interface IPlayStreamsWithConfidence : IPlayStreams
{
    [Obsolete("SilverAudioPlayer doesn't make use of this yet")]

    public Confidence CanPlayStream(WrappedStream stream);
}
[Obsolete("SilverAudioPlayer doesn't make use of this yet")]

public enum Confidence : byte
{
    /// <summary>
    /// The player is unsure whether it can play the stream
    /// </summary>
    Unknown=0, 
    /// <summary>
    /// The player is sure that it is NOT ABLE to play the stream
    /// </summary>
    ConfidentNo,
    /// <summary>
    /// The player thinks it might be able to play the stream but depending on other factors that have not been considered it might fail to do so
    /// </summary>
    SemiConfident,
    /// <summary>
    /// The player KNOWS it IS ABLE to play the stream
    /// </summary>
    Confident
}
public interface IPlayLancer : IPlayStreams
{
    /// <summary>
    /// Queues the next stream for seamless playback inside the current player.
    /// </summary>
    /// <param name="stream">The next stream to play.</param>
    /// <returns>True when the stream was queued successfully.</returns>
    bool EnqueueNextStream(WrappedStream stream);

    /// <summary>
    /// Raised when the player switches to the next queued stream.
    /// </summary>
    event EventHandler? NextStreamStarted;
}
public interface ISelector
{
    public int CurrentPattern{get;}
    public void SetPattern(int pattern);
    public int NumberOfPatterns {get;}
}
public interface ILoop
{
    public bool CanLoop{get;}
      public void EnableLoop();
    public void DisableLoop();
}
public interface IPlayerLoop : IPlay,ILoop
{
  
}
public interface IPlayWithSelector : IPlay,ISelector
{
}
public interface IPlay : IDisposable
{
    /// <summary>
    ///     Starts playing the loaded file.
    /// </summary>
    void Play();

    /// <summary>
    ///     Stops playing the loaded file and unloads the file.
    /// </summary>
    void Stop();

    /// <summary>
    ///     Pauses the player.
    /// </summary>
    void Pause();

    /// <summary>
    ///     Resumes the player.
    /// </summary>
    void Resume();

    /// <summary>
    ///     Returns the count of how many channels this audio file has
    /// </summary>
    /// <returns>A number that is larger than 0</returns>
    uint? ChannelCount();

    /// <summary>
    ///     Sets the volume level of the audio player.
    /// </summary>
    /// <param name="volume">The volume level, a byte ranging from 0-100.</param>
    void SetVolume(byte volume);

    /// <summary>
    ///     Gets the position of the audio player.
    /// </summary>
    /// <returns>A timespan with the current position of the player</returns>
    TimeSpan GetPosition();
    /// <summary>
    /// Sets the position of the player
    /// </summary>
    /// <param name="position">The desired position</param>
    void SetPosition(TimeSpan position);
    /// <summary>
    /// Gets the playback state of the player
    /// </summary>
    /// <returns>the playback state of the player</returns>

    PlaybackState? GetPlaybackState();

    /// <summary>
    /// Gets the length of the currently playing track
    /// </summary>
    /// <returns>the length of the currently playing track or null if its unknown or a live stream</returns>
    TimeSpan? Length();
    /// <summary>
    /// Gets the sample rate of the currently playing track (if applicable)
    /// </summary>
    /// <returns>the sample rate or null</returns>

    long? GetSampleRate();
    /// <summary>
    /// Gets the bit depth of the currently playing track (if applicable)
    /// </summary>
    /// <returns>the bit depth or null</returns>
    uint? GetBitsPerSample();
    /// <summary>
    /// Event which should be invoked when the track is stopped or reaches the end
    /// </summary>

    event EventHandler<object> TrackEnd;
    /// <summary>
    /// Event which should be invoked when the player is paused
    /// </summary>

    event EventHandler<object> TrackPause;
    /// <summary>
    /// Event to invoke when played has been seeked
    /// </summary>
    event EventHandler<TimeSpan> PositionChanged;

    /// <summary>
    /// Query if a track is loaded or if the player is empty
    /// </summary>
    bool IsTrackLoaded {get;} 
}


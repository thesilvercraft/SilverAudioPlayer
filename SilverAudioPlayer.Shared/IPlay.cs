namespace SilverAudioPlayer.Shared
{
    public interface IPlayStreams : IPlay
    {
        /// <summary>
        /// Loads a new stream to be played using the player
        /// </summary>
        /// <param name="stream">The stream to be loaded</param>
        void LoadStream(WrappedStream stream);
    }

   

    public interface IPlay
    {
        /// <summary>
        /// Starts playing the loaded file.
        /// </summary>
        void Play();

        /// <summary>
        /// Stops playing the loaded file and unloads the file.
        /// </summary>
        void Stop();

        /// <summary>
        /// Pauses the player.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes the player.
        /// </summary>
        void Resume();

        /// <summary>
        /// Returns the count of how many channels this audio file has
        /// </summary>
        /// <returns></returns>
        uint? ChannelCount();

        /// <summary>
        /// Sets the volume level of the audio player.
        /// </summary>
        /// <param name="volume">The volume level, a byte ranging from 1-100.</param>
        void SetVolume(byte volume);

        /// <summary>
        /// Gets the position of the audio player.
        /// </summary>
        /// <returns></returns>
        TimeSpan GetPosition();

        void SetPosition(TimeSpan position);

        PlaybackState? GetPlaybackState();

        TimeSpan? Length();

        long? GetSampleRate();

        uint? GetBitsPerSample();

        event EventHandler<object> TrackEnd;

        event EventHandler<object> TrackPause;
    }

    public interface ICanTellIfICanPlayAFile
    { }
}
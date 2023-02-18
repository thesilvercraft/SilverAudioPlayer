namespace SilverAudioPlayer.Shared;

public interface IMusicStatusInterface : IDisposable, ICodeInformation
{
    void StartIPC(IMusicStatusInterfaceListener listener);
    void StopIPC(IMusicStatusInterfaceListener listener);



   
}
public interface IMusicStatusInterfaceListenerAdmin: IMusicStatusInterfaceListener
{
    public void TrackChangedNotification(Song? currentSong);
    public void PlayerStateChanged(PlaybackState state);
}
public interface IMusicStatusInterfaceListener : IPlayerEnviroment
{

     void Play();

    void Pause();

    void PlayPause();

    void Stop();

    void Next();

    void Previous();

    void SetVolume(byte volume);

    byte GetVolume();

    Song? GetCurrentTrack();

    ulong GetDuration();

    void SetPosition(ulong position);

    ulong GetPosition();

    event EventHandler<Song> TrackChangedNotification;

    event EventHandler<PlaybackState> PlayerStateChanged;

    PlaybackState GetState();

    event EventHandler<IMusicStatusInterface> StateChangedNotification;

    event EventHandler<IMusicStatusInterface> RepeatChangedNotification;

    RepeatState GetRepeat();

    void  SetRepeat(RepeatState state);

    event EventHandler<IMusicStatusInterface> ShutdownNotiifcation;

    event EventHandler<IMusicStatusInterface> ShuffleChangedNotification;

    bool GetShuffle();

    void SetShuffle(bool shuffle);

    event EventHandler<IMusicStatusInterface> RatingChangedNotification;

    void  SetRating (byte rating);

    event EventHandler<IMusicStatusInterface> CurrentTrackNotification;

    event EventHandler<IMusicStatusInterface> CurrentLyricsNotification;

    event EventHandler<IMusicStatusInterface> NewLyricsNotification;

    event EventHandler<IMusicStatusInterface> NewCoverNotification;

    string GetLyrics();
}
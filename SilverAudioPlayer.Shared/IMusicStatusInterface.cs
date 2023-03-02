namespace SilverAudioPlayer.Shared;

public interface IMusicStatusInterface : IDisposable, ICodeInformation
{
    void StartIPC(IMusicStatusInterfaceListener listener);
    void StopIPC(IMusicStatusInterfaceListener listener);




}
public interface IMusicStatusInterfaceListenerAdmin : IMusicStatusInterfaceListener
{
    public void FireTrackChangedNotification(Song? currentSong);
    public void FirePlayerStateChanged(PlaybackState state);
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



    RepeatState GetRepeat();

    void SetRepeat(RepeatState state);



    bool GetShuffle();

    void SetShuffle(bool shuffle);


    void SetRating(byte rating);





    string GetLyrics();
}
namespace SilverAudioPlayer.Shared;

public interface IPlayStreamProviderListener: IPlayerEnviroment
{
    void LoadSong(WrappedStream s);
    public void ProcessFiles(IEnumerable<string> files);

    void LoadSongs(IEnumerable<WrappedStream> streams);
    IEnumerable<string> FilterFiles(IEnumerable<string> files);
}
namespace SilverAudioPlayer.Shared;

public interface IPlayStreamProvider : ICodeInformation
{
    public IPlayStreamProviderListner ProviderListner { set; }
    void ShowGui();
}

public interface IPlayStreamProviderListner
{
    void LoadSong(WrappedStream s);

    void LoadSongs(IEnumerable<WrappedStream> streams);
    IPlayerEnviroment GetEnviroment();
}
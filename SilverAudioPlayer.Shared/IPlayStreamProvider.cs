namespace SilverAudioPlayer.Shared
{
    public interface IPlayStreamProvider : ICodeInformation
    {
        void ShowGui();

        public IPlayStreamProviderListner ProviderListner {  set; }
    }

    public interface IPlayStreamProviderListner
    {
        void LoadSong(WrappedStream s);

        void LoadSongs(IEnumerable<WrappedStream> streams);
        IPlayerEnviroment GetEnviroment();
    }
}
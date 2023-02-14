namespace SilverAudioPlayer.Shared;

public interface ISyncEnvironmentListener : IPlayerEnviroment
{
    
    public Task<List<Song>?> GetQueue();
    //TODO remuxing support
}
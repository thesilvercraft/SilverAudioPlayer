namespace SilverAudioPlayer.Shared;

public interface ISyncEnvironmentListener : IPlayerEnvironment
{
    
    public Task<List<Song>?> GetQueue();
    //TODO remuxing support
}
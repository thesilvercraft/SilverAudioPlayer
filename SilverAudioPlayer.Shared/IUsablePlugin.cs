namespace SilverAudioPlayer.Shared;

public interface IUsablePlugin<TListenerEnv> : ICodeInformation where TListenerEnv : IPlayerEnviroment
{
    void Use(TListenerEnv env);

}
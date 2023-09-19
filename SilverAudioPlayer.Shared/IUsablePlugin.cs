namespace SilverAudioPlayer.Shared;

public interface IUsablePlugin<TListenerEnv> : ICodeInformation where TListenerEnv : IPlayerEnvironment
{
    void Use(TListenerEnv env);

}
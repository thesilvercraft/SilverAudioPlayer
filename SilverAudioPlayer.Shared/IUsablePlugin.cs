using SilverCraft.Ekorensis;

namespace SilverAudioPlayer.Shared;

public interface IUsablePlugin<TListenerEnv> : ICodeInformation where TListenerEnv : IPlayerEnvironment
{
    void Use(TListenerEnv env);

}

public interface IConfigurableUiPlugin
{
    void ConfigureUi(IConfigurableListener env);
}
namespace SilverAudioPlayer.Shared;

public class CommandPaletteItem(string name, string id, Action<IPlayerEnvironment> exec) : ICommandPaletteItem
{
    public string HumanName { get; } = name;
    public string Id { get; } = id;
    Action<IPlayerEnvironment> _exec = exec;
    public void Execute(IPlayerEnvironment environment)
    {
        _exec(environment);
    }
}
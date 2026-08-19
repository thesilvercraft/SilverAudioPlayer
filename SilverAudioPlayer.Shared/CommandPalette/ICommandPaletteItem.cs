namespace SilverAudioPlayer.Shared;

public interface ICommandPaletteItem
{
    public string HumanName { get; }
    public string Id { get; }
    public void Execute(IPlayerEnvironment environment);
}
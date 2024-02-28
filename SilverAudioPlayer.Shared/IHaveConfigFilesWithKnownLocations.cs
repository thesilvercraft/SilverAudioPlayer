namespace SilverAudioPlayer.Shared;
/// <summary>
/// An object which stores its config files itself and provides their location
/// </summary>
public interface IHaveConfigFilesWithKnownLocations
{
    public string[] KnownConfigFileLocations { get; }
}

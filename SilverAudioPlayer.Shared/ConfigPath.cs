namespace SilverAudioPlayer.Shared;

public interface IConfigPathProvider
{
    string GetPath(string subpath);
}
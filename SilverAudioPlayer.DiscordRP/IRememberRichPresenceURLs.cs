using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MusicStatusInterface.DiscordRP;

public interface IRememberRichPresenceURLs
{
    string? GetURL(Song? track, IMusicStatusInterfaceListener Env);
}
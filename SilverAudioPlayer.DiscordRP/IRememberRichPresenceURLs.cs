using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.DiscordRP;

public interface IRememberRichPresenceURLs
{
    string? GetURL(Song? track, IMusicStatusInterfaceListener Env);
}
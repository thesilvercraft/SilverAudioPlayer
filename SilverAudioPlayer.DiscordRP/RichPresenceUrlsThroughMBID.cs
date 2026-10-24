using System;
using System.Net.Http;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MusicStatusInterface.DiscordRP;

public class RichPresenceUrlsThroughMBID : IRememberRichPresenceURLs
{
    private const string BaseUrl = "https://coverartarchive.org";

    public RichPresenceUrlsThroughMBID(IConfigPathProvider configPathProvider, HttpClient hclient)
    {
    }

    public string? GetURL(Song? track, IMusicStatusInterfaceListener Env)
    {
        if (track?.Metadata?.Entries == null)
            return null;

        if (track.Metadata.Entries.TryGetValue("MUSICBRAINZ_RELEASEGROUPID", out var rgId) 
            && !string.IsNullOrWhiteSpace(rgId) 
            && Guid.TryParse(rgId, out _))
        {
            return $"{BaseUrl}/release-group/{rgId}/front";
        }

        if (track.Metadata.Entries.TryGetValue("MUSICBRAINZ_RELEASEID", out var rId) 
            && !string.IsNullOrWhiteSpace(rId) 
            && Guid.TryParse(rId, out _))
        {
            return $"{BaseUrl}/release/{rId}/front";
        }

        return null;
    }
}
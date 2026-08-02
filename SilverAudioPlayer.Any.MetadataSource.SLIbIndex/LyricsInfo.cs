using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.SLIbIndex;
public class LyricsInfo(string? lyrics,  IList<LyricPhrase>? syncedLyrics)
{
    public string? lyrics { get; init; } = lyrics;
    public IList<LyricPhrase>? syncedLyrics { get; init; } = syncedLyrics;
}
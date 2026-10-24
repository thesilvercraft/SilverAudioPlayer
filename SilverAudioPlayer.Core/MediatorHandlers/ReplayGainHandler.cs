using System.Globalization;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;

namespace SilverAudioPlayer.Core;

public class ReplayGainHandler
{
    public bool PreferAlbumReplayGain { get; set; } = false;

    public void HandleMediatorQuery(QueryEventArgs query, Song? currentSong)
    {
        if (query.Query is not RequestReplayGainInfo) return;
        if (currentSong is not { Metadata: { Entries.Count: > 0 } metadata }) return;

        var entries = metadata.Entries;

        float? TryGetReplayGainPeak(bool isAlbum)
        {
            if (entries.TryGetValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_PEAK", out var peakStr) &&
                float.TryParse(peakStr, CultureInfo.InvariantCulture, out var peakVal))
            {
                return peakVal;
            }
            return null;
        }

        bool TryGetReplayGain(bool isAlbum, out float gain)
        {
            gain = 0;
            if (entries.TryGetValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_GAIN", out var gainStr))
            {
                var cleanVal = gainStr.Replace("dB", "", StringComparison.OrdinalIgnoreCase).Trim();
                return float.TryParse(cleanVal, CultureInfo.InvariantCulture, out gain);
            }
            return false;
        }

        var primaryIsAlbum = PreferAlbumReplayGain;
        var secondaryIsAlbum = !PreferAlbumReplayGain;

        var album = primaryIsAlbum;
        var rgSuccess = TryGetReplayGain(primaryIsAlbum, out var gainValue);

        if (!rgSuccess)
        {
            album = secondaryIsAlbum;
            rgSuccess = TryGetReplayGain(secondaryIsAlbum, out gainValue);
        }

        var peak = TryGetReplayGainPeak(album);

        query.Handled = true;
        query.Result = new ReplayGainInfo
        {
            Album = album,
            Gain = gainValue,
            Peak = peak,
            Success = rgSuccess
        };
    }
}

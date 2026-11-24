using System.Globalization;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;

namespace SilverAudioPlayer.Core.MediatorHandlers;

public class ReplayGainHandler
{
    public bool PreferAlbumReplayGain { get; set; } = false;

    public void HandleMediatorQuery(QueryEventArgs query, Song? currentSong)
    {
        if (query.Query is not RequestReplayGainInfo) return;
        if (currentSong is not { Metadata: { Entries.Count: > 0 } metadata }) return;

        var entries = metadata.Entries;

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
        return;

        string? TryGetEntryValue(string key)
        {
            if (entries.TryGetValue(key, out var exact))
            {
                return exact;
            }

            foreach (var kvp in entries)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        float? TryGetReplayGainPeak(bool isAlbum)
        {
            var peakStr = TryGetEntryValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_PEAK");
            if (peakStr != null && float.TryParse(peakStr, CultureInfo.InvariantCulture, out var peakVal))
            {
                return peakVal;
            }
            return null;
        }

        bool TryGetReplayGain(bool isAlbum, out float gain)
        {
            gain = 0;
            var gainStr = TryGetEntryValue($"REPLAYGAIN_{(isAlbum ? "ALBUM" : "TRACK")}_GAIN");
            if (gainStr == null) return false;
            var cleanVal = gainStr.Replace("dB", "", StringComparison.OrdinalIgnoreCase).Trim();
            return float.TryParse(cleanVal, CultureInfo.InvariantCulture, out gain);
        }
    }
}

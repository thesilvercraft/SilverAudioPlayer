using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;

namespace SilverAudioPlayer.Core;

public class MusicBrainzHandler
{
    private static readonly char[] IdSeparators = [';', '/', ',', '\0'];

    public void HandleMediatorQuery(QueryEventArgs query, Song? currentSong)
    {
        if (query.Query is not RequestMusicBrainzInfo req) return;

        var targetSong = req.Song ?? currentSong;
        if (targetSong is not { Metadata.Entries.Count: > 0 }) return;

        var entries = targetSong.Metadata.Entries;

        string? TryGetEntry(params string[] keys)
        {
            foreach (var key in keys)
            {
                foreach (var entry in entries
                             .Where(entry=>
                                 string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase) &&
                                 !string.IsNullOrWhiteSpace(entry.Value)))
                {
                    return entry.Value.Trim();
                }
            }
            return null;
        }

        Guid? TryGetGuid(params string[] keys)
        {
            var val = TryGetEntry(keys);
            if (val != null && Guid.TryParse(val, out var guid))
            {
                return guid;
            }
            return null;
        }

        List<Guid> TryGetGuidList(params string[] keys)
        {
            var val = TryGetEntry(keys);
            if (string.IsNullOrWhiteSpace(val)) return [];

            var results = new List<Guid>();
            var tokens = val.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (Guid.TryParse(token, out var guid) && !results.Contains(guid))
                {
                    results.Add(guid);
                }
            }
            return results;
        }

        var trackId = TryGetGuid("MUSICBRAINZ_TRACKID");
        var recordingId = TryGetGuid("MUSICBRAINZ_RECORDINGID");
        var albumId = TryGetGuid("MUSICBRAINZ_ALBUMID");
        var releaseId = TryGetGuid("MUSICBRAINZ_RELEASEID");
        var artistIds = TryGetGuidList("MUSICBRAINZ_ARTISTID");
        var albumArtistIds = TryGetGuidList("MUSICBRAINZ_ALBUMARTISTID");
        var releaseTrackId = TryGetGuid("MUSICBRAINZ_RELEASETRACKID");
        var releaseGroupId = TryGetGuid("MUSICBRAINZ_RELEASEGROUPID");
        var workId = TryGetGuid("MUSICBRAINZ_WORKID");
        var originalAlbumId = TryGetGuid("MUSICBRAINZ_ORIGINALALBUMID");
        var originalArtistId = TryGetGuid("MUSICBRAINZ_ORIGINALARTISTID");
        var discId = TryGetEntry("MUSICBRAINZ_DISCID");

        var rawMbEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in entries)
        {
            if (kvp.Key.StartsWith("MUSICBRAINZ_", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Value))
            {
                rawMbEntries[kvp.Key] = kvp.Value;
            }
        }

        var hasAny = trackId != null || albumId != null || artistIds.Count > 0 ||
                     albumArtistIds.Count > 0 || releaseTrackId != null ||
                     releaseGroupId != null || workId != null || originalAlbumId != null ||
                     originalArtistId != null || discId != null || rawMbEntries.Count > 0;

        if (!hasAny) return;
        query.Handled = true;
        query.Result = new MusicBrainzInfo
        {
            Success = true,
            TrackId = trackId,
            RecordingId = recordingId,
            AlbumId = albumId,
            ReleaseId = releaseId,
            ArtistId = artistIds.Count > 0 ? artistIds[0] : null,
            AlbumArtistId = albumArtistIds.Count > 0 ? albumArtistIds[0] : null,
            ArtistIds = artistIds,
            AlbumArtistIds = albumArtistIds,
            ReleaseTrackId = releaseTrackId,
            ReleaseGroupId = releaseGroupId,
            WorkId = workId,
            OriginalAlbumId = originalAlbumId,
            OriginalArtistId = originalArtistId,
            DiscId = discId,
            RawEntries = rawMbEntries
        };
    }
}

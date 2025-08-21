using Jellyfin.Sdk.Generated.Models;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class JellyfinMeta(BaseItemDto dto, IList<LyricPhrase>? slyrics) : IMetadata
{
    public BaseItemDto _dto = dto;

    public Guid? Dto => _dto.Id;
    public void Dispose()
    {
        _dto = null;
    }

    public string? Title =>_dto.Name;
    public string? Artist => _dto.AlbumArtist;
    public string? Album=>_dto.Album;
    public string? Genre =>  _dto.Genres==null ? null : string.Join(';', _dto.Genres);
    public int? Year => _dto.ProductionYear;
    public ulong? Bitrate => null;
    public ulong? SampleRate  => null;
    public uint? Channels  => null;
    public int? TrackNumber  => null;//=> _dto.IndexNumber;
    public int? DiscNumber => null;
    public string[]? Comments  => null;
    public IDictionary<string, string>? Entries  => null;
    public double? Duration  => null;
    public string? Lyrics  => null;
    public IList<LyricPhrase>? SyncedLyrics { get; private set; } = slyrics;
    public IReadOnlyList<IPicture>? Pictures  => null;
}
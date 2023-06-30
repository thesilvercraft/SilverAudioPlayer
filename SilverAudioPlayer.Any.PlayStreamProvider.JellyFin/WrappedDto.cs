using Avalonia.Media.Imaging;
using Jellyfin.Sdk;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class WrappedDto
{
    public BaseItemDto dto;

    public WrappedDto(BaseItemDto dto, WrappedStream? ws = null)
    {
        this.dto = dto;
        if (ws != null) Cover = Bitmap.DecodeToHeight(ws.GetStream(), 200);
    }

    public string Name => dto.Name;
    public string AlbumArtist => dto.AlbumArtist;
    public bool? IsFolder => dto.IsFolder;
    public int? IndexNumber => dto.IndexNumber;
    public Bitmap? Cover { get; set; }
}
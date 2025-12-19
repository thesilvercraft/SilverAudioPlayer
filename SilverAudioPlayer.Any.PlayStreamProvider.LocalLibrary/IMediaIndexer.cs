using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Controls.Embedding.Offscreen;
using Avalonia.Media.Imaging;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SLibIndex;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;

public class IndexShowable
{
    public string Name { get; set; }
    public string AlbumArtist { get; set; }
    public string? ImageLoc { get; set; } = null;

    public Bitmap? Cover => DecodeBitmap();
    Bitmap? DecodeBitmap()
    {
        if (b != null)
        {
            return b;
        }
        if (ImageLoc !=null && (ImageLoc.StartsWith("http://") || ImageLoc.StartsWith("https://")))
        {
            //todo

        }
        else if (ImageLoc !=null && !string.IsNullOrWhiteSpace(ImageLoc))
        {
            return new Bitmap(ImageLoc);
        }
        return null;
    }
    Bitmap b;
    RelianceOnShared<Bitmap> Reliance;

}
public class IndexAlbum : IndexShowable
{
}
public class IndexSong : IndexShowable
{
    public IndexSong(string loc, IMetadata metadata, IPlayStreamProviderListener env)
    {
        Loc = loc;
        Album = metadata.Album;
        LastChecked = DateTime.UtcNow;
        ImageLoc = ((IndexedTrack)metadata).Art;
        Name = metadata.Title;
        AlbumArtist = metadata.Artist;
    }
    public string Album { get; set; }
    public string Loc { get; set; }
    public DateTime LastChecked { get; set; }
    public bool HasChanged() => !Exists() || (File.GetLastWriteTimeUtc(Loc) > LastChecked);
    public bool Exists() => File.Exists(Loc);

}

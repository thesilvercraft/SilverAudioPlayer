using System.Composition;
using System.Diagnostics;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

[Export(typeof(IPlaylistReader))]
public class SPLPlaylistParser : IPlaylistReader
{
    public string Name => "Silverplaylist reader";
    public string Description => "a";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(SPLPlaylistParser).Assembly.GetName().Version; 
    public string Licenses => "GPL3";
    public List<Tuple<Uri, URLType>>? Links { get; } = new();
    public IReadOnlyList<MimeType> SupportedMimes { get; } = new[] { KnownMimes.SplMime };

    public bool TryParse(WrappedStream stream, out IPlaylist? playlist)
    {
        try
        {
            using var fs = stream.GetStream();
            var splylist = SilverPlaylistHelper.DeserializeBrotli(fs);
            if (splylist != null)
            {
                playlist = new SilverPlayListI (splylist);
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        playlist = null;
        return false;

    }
}
class SilverPlayListI : IPlaylist
{
    public SilverPlayListI(SilverPlayList s)
    {
        if (s.CurrentIndex != null)
        {
            IndexOfActive = int.Parse(s.CurrentIndex);
        }
        if (s.CurrentIndexPosition != null)
        {
            PositionOfActive = double.Parse(s.CurrentIndexPosition);
        }
        Tracks = s.Song.Select(x => new SilverPlayListTrackI(x)).ToList();
    }

    public IEnumerable<ITrackLoadInfo> Tracks { get; } 
    public int? IndexOfActive { get; }
    public double? PositionOfActive { get; }
}

class SilverPlayListTrackI : ITrackLoadInfo
{
    public SilverPlayListTrackI(SerializedSong s)
    {
        URL = s.Sources.Source[0].Text;
        Source = s.Sources.Source[0].Type;
    }
    public string URL { get; set; }
    public string Source { get; }
}
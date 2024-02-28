using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

class SilverPlayListTrackI : ITrack
{
    public SilverPlayListTrackI(SerializedSong s)
    {
        TrackLoadInfos = s.Sources.Source.Select(x => new SilverPlaylistTrackLoadInfo(x)).ToList();
    }
    public IEnumerable<ITrackLoadInfo> TrackLoadInfos { get; }
}
public class SilverPlaylistTrackLoadInfo : ITrackLoadInfo
{
    public SilverPlaylistTrackLoadInfo(Source s)
    {
        URL = s.Text;
        Source = s.Type;
    }
    public string URL { get; set; }
    public string Source { get; }
}
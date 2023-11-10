using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

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
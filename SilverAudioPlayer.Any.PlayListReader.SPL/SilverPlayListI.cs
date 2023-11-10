using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

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
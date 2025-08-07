using System.Globalization;
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
            PositionOfActive = double.Parse(s.CurrentIndexPosition, CultureInfo.InvariantCulture);
        }
        Tracks = s.Song.Select(x => new SilverPlayListTrackI(x)).ToList();
    }

    public IEnumerable<ITrack> Tracks { get; } 
    public int? IndexOfActive { get; internal set;}
    public double? PositionOfActive { get;internal set;}
}
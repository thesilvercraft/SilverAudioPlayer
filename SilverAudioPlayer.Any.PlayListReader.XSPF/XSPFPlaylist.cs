using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayListReader.XSPF;

public class XSPFPlaylist : IPlaylist
{
    public XSPFPlaylist(IEnumerable<ITrack> tracks, int? indexOfActive = default, double? positionOfActive = default)
    {
        Tracks = tracks;
        IndexOfActive = indexOfActive;
        PositionOfActive = positionOfActive;
    }

    public IEnumerable<ITrack> Tracks { get; }
    public int? IndexOfActive { get; }
    public double? PositionOfActive { get; }
}
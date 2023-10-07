using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayListReader.M3U;

public class M3UPlaylist : IPlaylist
{
    public M3UPlaylist(IEnumerable<ITrackLoadInfo> tracks, int? indexOfActive = default, double? positionOfActive = default)
    {
        Tracks = tracks;
        IndexOfActive = indexOfActive;
        PositionOfActive = positionOfActive;
    }

    public IEnumerable<ITrackLoadInfo> Tracks { get; }
    public int? IndexOfActive { get; }
    public double? PositionOfActive { get; }
}

public class M3UTrack : ITrackLoadInfo
{
    public M3UTrack(string url, string source)
    {
        URL = url;
        Source = source;
    }

    public string URL { get; set; }
    public string Source { get; }
}
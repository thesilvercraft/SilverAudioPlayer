using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;

namespace SilverAudioPlayer.Any.PlayListReader.M3U;

public class M3UPlaylist : IPlaylist
{
    public M3UPlaylist(IEnumerable<ITrack> tracks, int? indexOfActive = default, double? positionOfActive = default)
    {
        Tracks = tracks;
        IndexOfActive = indexOfActive;
        PositionOfActive = positionOfActive;
    }

    public IEnumerable<ITrack> Tracks { get; }
    public int? IndexOfActive { get; }
    public double? PositionOfActive { get; }
}

public class M3UTrack : ITrack
{
    public M3UTrack(string url, string source)
    {
        track = new M3UTrackLoadInfo(url, source);
    }

    private M3UTrackLoadInfo track;
    public IEnumerable<ITrackLoadInfo> TrackLoadInfos => new[] { track };
}

public class M3UTrackLoadInfo : ITrackLoadInfo
{
    public M3UTrackLoadInfo(string url, string source)
    {
        URL = url;
        Source = source;
    }

    public string URL { get; set; }
    public string Source { get; }
}
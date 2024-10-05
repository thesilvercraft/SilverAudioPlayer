using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayListReader.XSPF;

public class XSPFTrack : ITrack
{
    public XSPFTrack(string url, string source)
    {
        track = new XSPFTrackLoadInfo(url, source);
    }

    private XSPFTrackLoadInfo track;
    public IEnumerable<ITrackLoadInfo> TrackLoadInfos => new[] { track };
}
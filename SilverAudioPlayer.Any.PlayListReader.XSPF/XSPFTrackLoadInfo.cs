using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayListReader.XSPF;

public class XSPFTrackLoadInfo : ITrackLoadInfo
{
    public XSPFTrackLoadInfo(string url, string source)
    {
        URL = url;
        Source = source;
    }

    public string URL { get; set; }
    public string Source { get; }
}
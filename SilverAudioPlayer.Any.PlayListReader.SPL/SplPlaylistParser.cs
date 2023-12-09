using System.Composition;
using System.Diagnostics;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

[Export(typeof(IPlaylistReader))]
public class SplPlaylistParser : IPlaylistReader
{
    public string Name => "SilverPlaylist reader";
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SplPlaylistParser).Assembly,
        "SilverAudioPlayer.Any.PlayListReader.SPL.icon.svg");
    public string Description => "A simple module that deserializes brotli encoded silverplaylist files";
    public Version? Version => typeof(SplPlaylistParser).Assembly.GetName().Version; 
    public string Licenses => "GPL3";
    public List<Tuple<Uri, URLType>>? Links { get; } = [];
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
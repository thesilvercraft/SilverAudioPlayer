using System.Composition;
using Jurassic;
using SilverAudioPlayer.Shared;
using VideoLibrary;

namespace SilverAudioPlayer.Any.PlayStreamProvider.VideoLib;
[Export(typeof(IPlayStreamProvider))]
public class VideoLibraryPlayStreamProvider :IPlayStreamProviderThatSupportsUrls
{
    private string[] Domains = new[]
    {
        "youtube.com","yewtu.be", "vid.puffyan.us", "inv.riverside.rocks", "invidio.xamh.de", "y.com.sb",
        "invidious.sethforprivacy.com","yt.artemislena.eu","invidious.tiekoetter.com","invidious.flokinet.to",
        "inv.bp.projectsegfau.lt","inv.vern.cc","inv.odyssey346.dev","invidious.snopyta.org","invidious.baczek.me",
        "invidious.drivet.xyz","yt.funami.tech","invidious.slipfox.xyz","invidious.dhusch.de","invidious.weblibre.org",
        "invidious.esmailelbob.xyz","invidious.namazso.eu","invidious.privacydev.net"
    };

    public string Name => "libVideo";
    public string Description => "libVideo provider";
    public WrappedStream? Icon =>null;
    public Version? Version => typeof(VideoLibraryPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "GPL3.0\nMIT";
    public List<Tuple<Uri, URLType>>? Links => null;
    public IPlayStreamProviderListner ProviderListner { get; set; }
    public void ShowGui()
    {
    }

    public bool IsUrlSupported(Uri given)
    {
        return (Domains.Any(x => given.Host.StartsWith(x)));
    }

    public async Task LoadUrlAsync(Uri given)
    {
        if(Domains.Any(x => given.Host.StartsWith(x)));
        {
            UriBuilder builder = new(given);
            builder.Host = Domains[0];
            given = builder.Uri;
        }
        var youTube = YouTube.Default;
        var video = await youTube.GetAllVideosAsync(given.ToString());
        ProviderListner.LoadSong(new WrappedHttpStream(video.Where(x => x.AudioBitrate > 1).OrderByDescending(x => x.AudioBitrate).First().Uri));
    }
}
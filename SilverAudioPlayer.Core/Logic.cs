using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.Core
{
    public class Logic
    {
        [ImportMany]
        public IEnumerable<IPlayProvider> PlayProviders { get; set; }

        [ImportMany]
        public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }
        [ImportMany]
        public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }
        [ImportMany]
        public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
        public Serilog.Core.Logger log { get; set; }
        public List<MimeType> PlayableMimes { get; set; }
        public IPlay? GetPlayerFromStream(WrappedStream stream)
        {
            var provider = PlayProviders?.FirstOrDefault(x => x.CanPlayFile(stream));
            return provider?.GetPlayer(stream);
        }

        public IMetadataProvider? GetMetadataProviderFromStream(WrappedStream stream)
        {
            return MetadataProviders?.FirstOrDefault(x => x.CanGetMetadata(stream));
        }

        public Task<Metadata?>? GetMetadataFromStream(WrappedStream stream)
        {
            return GetMetadataProviderFromStream(stream)?.GetMetadata(stream);
        }
        public IEnumerable<string> FilterFiles(IEnumerable<string> files) => files.Where(x => !(
           x.EndsWith(".png")
        || x.EndsWith(".txt")
        || x.EndsWith(".docx")
        || x.EndsWith(".pdf")
        || x.EndsWith(".csv")
        || x.EndsWith(".jpg")
        || x.EndsWith(".lnk")
        || x.EndsWith(".md")
        || x.EndsWith(".zip")
        || x.EndsWith(".7z")
        || x.EndsWith(".rar")
        || x.EndsWith(".exe")
        || x.EndsWith(".dll")
        || x.EndsWith(".json")
        || x.EndsWith(".toml")
        || x.EndsWith(".yaml")
        || x.EndsWith(".xml")
        || x.EndsWith(".nfo")
        || x.EndsWith(".html")
        || x.EndsWith(".m3u")
        || x.EndsWith(".xmp")
        || x.EndsWith(".log")
        || x.EndsWith(".gif")
        || x.EndsWith(".cue")
        || x.EndsWith(".m3u")
        || x.EndsWith(".fpl")
        || x.EndsWith(".htm")
        || x.EndsWith(".pkf")
        || x.EndsWith(".db")
        || x.EndsWith(".webp")
        || x.EndsWith(".spotdl-cache")
        ));
    }
}
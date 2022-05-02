using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.Core
{
    public class Logic
    {
        [ImportMany(typeof(IPlayProvider))]
        public IEnumerable<Lazy<IPlayProvider>>? PlayProviders;

        [ImportMany(typeof(IMetadataProvider))]
        public IEnumerable<Lazy<IMetadataProvider>>? MetadataProviders;

        [ImportMany(typeof(IMusicStatusInterface))]
        public IEnumerable<Lazy<IMusicStatusInterface>>? MusicStatusInterfaces;

        public Serilog.Core.Logger log { get; set; }

        public IPlay? GetPlayerFromStream(WrappedStream stream)
        {
            var provider = PlayProviders?.FirstOrDefault(x => x.IsValueCreated && x.Value.CanPlayFile(stream));
            return provider?.Value?.GetPlayer(stream);
        }

        public IMetadataProvider? GetMetadataProviderFromStream(WrappedStream stream)
        {
            return MetadataProviders?.FirstOrDefault(x => x.IsValueCreated && x.Value.CanGetMetadata(stream))?.Value;
        }

        public Task<Metadata?>? GetMetadataFromStream(WrappedStream stream)
        {
            return GetMetadataProviderFromStream(stream)?.GetMetadata(stream);
        }

        public IEnumerable<string> FilterFiles(IEnumerable<string> files)
        {
            return files.Where(x => !x.EndsWith(".png") && !x.EndsWith(".txt") && !x.EndsWith(".pdf") && !x.EndsWith(".jpg") && !x.EndsWith(".lnk") && !x.EndsWith(".md") && !x.EndsWith(".zip") && !x.EndsWith(".7z") && !x.EndsWith(".rar") && !x.EndsWith(".exe") && !x.EndsWith(".dll") && !x.EndsWith(".json") && !x.EndsWith(".toml") && !x.EndsWith(".yaml") && !x.EndsWith(".xml") && !x.EndsWith(".nfo") && !x.EndsWith(".html") && !x.EndsWith(".m3u") && !x.EndsWith(".xmp") && !x.EndsWith(".log") && !x.EndsWith(".gif") && !x.EndsWith(".cue") && !x.EndsWith(".db"));
        }
    }
}
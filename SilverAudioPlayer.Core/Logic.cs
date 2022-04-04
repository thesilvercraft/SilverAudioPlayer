using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.Core
{
    public class Logic
    {
        [ImportMany(typeof(IPlayProvider))]
        public IEnumerable<Lazy<IPlayProvider>>? Providers;

        [ImportMany(typeof(IMetadataProvider))]
        public IEnumerable<Lazy<IMetadataProvider>>? MetadataProviders;

        public bool CanGetPlayerFromURI(string URI)
        {
            return Providers?.Any(x => x.IsValueCreated && x.Value.CanPlayFile(URI)) == true;
        }

        public bool CanGetMetadataFromURI(string URI)
        {
            return MetadataProviders?.Any(x => x.IsValueCreated && x.Value.CanGetMetadata(URI)) == true;
        }

        public IPlay? GetPlayerFromURI(string URI)
        {
            var provider = Providers?.FirstOrDefault(x => x.IsValueCreated && x.Value.CanPlayFile(URI));
            return provider?.Value?.GetPlayer(URI);
        }

        public IMetadataProvider? GetMetadataProviderFromURI(string URI)
        {
            return MetadataProviders?.FirstOrDefault(x => x.IsValueCreated && x.Value.CanGetMetadata(URI))?.Value;
        }

        public Task<Metadata?>? GetMetadataFromURI(string URI)
        {
            return GetMetadataProviderFromURI(URI)?.GetMetadata(URI);
        }

        public IEnumerable<string> FilterFiles(IEnumerable<string> files)
        {
            return files.Where(x => !x.EndsWith(".png") && !x.EndsWith(".txt") && !x.EndsWith(".pdf") && !x.EndsWith(".jpg") && !x.EndsWith(".lnk") && !x.EndsWith(".md") && !x.EndsWith(".zip") && !x.EndsWith(".7z") && !x.EndsWith(".rar") && !x.EndsWith(".exe") && !x.EndsWith(".dll") && !x.EndsWith(".json") && !x.EndsWith(".toml") && !x.EndsWith(".yaml") && !x.EndsWith(".xml") && !x.EndsWith(".nfo") && !x.EndsWith(".html") && !x.EndsWith(".m3u") && !x.EndsWith(".xmp") && !x.EndsWith(".log") && !x.EndsWith(".gif") && !x.EndsWith(".cue") && !x.EndsWith(".db"));
        }
    }
}
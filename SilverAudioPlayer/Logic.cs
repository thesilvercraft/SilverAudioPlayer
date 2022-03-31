using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer
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
    }
}
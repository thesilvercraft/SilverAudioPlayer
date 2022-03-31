using SilverAudioPlayer.Shared;
using ATL;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.MetadataSource.Z440AtlCore
{
    [Export(typeof(IMetadataProvider))]
    public class AtlCoreMetadataProvider : IMetadataProvider
    {
        public bool CanGetMetadata(string path)
        {
            return new Track(path).AudioFormat != null;
        }

        public Task<Metadata?> GetMetadata(string path)
        {
            return Task.FromResult((Metadata?)new AtlCoreMetadata(new(path)));
        }
    }
}
using SilverAudioPlayer.Shared;
using ATL;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.MetadataSource.Z440AtlCore
{
    [Export(typeof(IMetadataProvider))]
    public class AtlCoreFileMetadataProvider : IMetadataProvider
    {
        public bool CanGetMetadata(WrappedStream stream)
        {
            return new Track(stream.RegenStream(), stream.MimeType).AudioFormat != null;
        }

        public Task<Metadata?> GetMetadata(WrappedStream stream)
        {
            return Task.FromResult((Metadata?)new AtlCoreMetadata(new(stream.RegenStream(), stream.MimeType)));
        }
    }
}
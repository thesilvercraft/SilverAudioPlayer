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
            using var s = stream.GetStream();
            return new Track(s, stream.MimeType).AudioFormat != null;
        }

        public Task<Metadata?> GetMetadata(WrappedStream stream)
        {
            using var s = stream.GetStream();
            return Task.FromResult((Metadata?)new AtlCoreMetadata(new(s, stream.MimeType)));
        }
    }
}
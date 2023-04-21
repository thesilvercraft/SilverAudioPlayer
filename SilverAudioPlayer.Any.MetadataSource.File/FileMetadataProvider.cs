using System.Composition;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.File;

[Export(typeof(IMetadataProvider))]
public class FileMetadataProvider : IMetadataProvider
{
    public string Name => "File Metadata Provider";

    public string Description => "Metadata Provider that provides from files";

    public WrappedStream Icon => null;

    public Version? Version => typeof(FileMetadataProvider).Assembly.GetName().Version;

    public string Licenses => "GPL";

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri(
                "https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.MetadataSource.File"),
            URLType.Code),
        
    };

    public bool CanGetMetadata(WrappedStream stream)
    {
        return stream is WrappedFileStream;
    }

    public Task<IMetadata?> GetMetadata(WrappedStream stream)
    {
        if (stream is WrappedFileStream s)
        {
            return Task.FromResult((IMetadata)new FileMetadata(s));
        }
        return Task.FromResult((IMetadata?)null);
    }
}
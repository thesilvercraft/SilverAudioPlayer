using System.Composition;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.File;

[Export(typeof(IMetadataProvider))]
public class FileMetadataProvider : IMetadataProvider
{
    public string Name => "File Metadata Provider";

    public string Description => "Metadata Provider that provides from files";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(FileMetadataProvider).Assembly,
        "SilverAudioPlayer.Any.MetadataSource.File.icon.svg");

    public Version? Version => typeof(FileMetadataProvider).Assembly.GetName().Version;

    public string Licenses => "SilverAudioPlayer.Any.MetadataSource.File is a part of the SilverAudioPlayer project and is licensed under the GPL3.0 license";

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri(ConstantCodeInfo.GetCurrentUrl()),
            URLType.Code),
        
    ];

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
using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class ATLCOREPicture : IPicture
{
    public ATLCOREPicture(PictureInfo i)
    {
        Cached = SharedMemoryStreamPoolInstance.Instance.GetFromByteArray(i.PictureData);
        Description = i.Description;
        PicType = (PictureType?)i.PicType;
        Reliance = new(Cached);
        Hash = i.PictureHash.ToString();
    }

    public void Dispose()
    {
        Cached = null;
        Reliance?.Dispose();
        GC.SuppressFinalize(this);
    }

    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;
    public string? Description { get; init; }
    public WrappedStream? Data => Cached.Stream;
    public PictureType? PicType { get; init; }
    public string? Hash { get; set; }
}

using ATL;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class ATLCOREPicture : IPicture
{
    public ATLCOREPicture(PictureInfo i)
    {
        (Cached, Reliance) = SharedMemoryStreamPoolInstance.Instance.AcquireFromByteArray(i.PictureData, copy: false);
        Description = i.Description;
        PicType = (PictureType?)i.PicType;
        Hash = i.PictureHash.ToString();
    }

    public void Dispose()
    {
        Reliance?.Dispose();
        Reliance = null;
        Cached = null;
        GC.SuppressFinalize(this);
    }

    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;
    public string? Description { get; }
    public WrappedStream? Data => Cached?.Stream;
    public PictureType? PicType { get; }
    /// <summary>
    /// AtlCore hash 
    /// </summary>
    public string? Hash { get; }
}
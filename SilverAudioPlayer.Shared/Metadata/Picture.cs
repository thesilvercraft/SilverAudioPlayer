namespace SilverAudioPlayer.Shared.Metadata;

public class Picture :IPicture
{
    public Picture(WrappedStream stream, PictureType? type=null, string? description =null)
    {
        Cached = SharedMemoryStreamPoolInstance.Instance.GetFromWrappedStream(stream);
        Reliance = new(Cached);
        Hash ??= Cached.Hash;
        PicType = type;
        Description = description;
    }
    public string? Description { get; init; }
    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;
   
    public WrappedStream? Data => Cached?.Stream;
    public PictureType? PicType { get;  }
    public string? Hash { get; }

    public void Dispose()
    {
        Reliance?.Dispose();
        GC.SuppressFinalize(this);
    }
}
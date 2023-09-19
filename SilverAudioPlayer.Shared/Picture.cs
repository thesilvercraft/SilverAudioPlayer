namespace SilverAudioPlayer.Shared;

public class Picture :IPicture
{
    public Picture(WrappedStream stream)
    {
        Cached = SharedMemoryStreamPoolInstance.Instance.GetFromWrappedStream(stream);
        Reliance = new(Cached);
        Hash ??= Cached.Hash;
    }
    public string? Description { get; init; }
    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;
   
    public WrappedStream? Data => Cached.Stream;
    public PictureType? PicType { get; set; }
    public string? Hash { get; set; }

    public virtual void Dispose()
    {
        Reliance?.Dispose();
        GC.SuppressFinalize(this);
    }
}
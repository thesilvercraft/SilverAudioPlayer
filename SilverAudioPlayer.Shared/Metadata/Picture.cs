namespace SilverAudioPlayer.Shared.Metadata;

public class Picture : IPicture
{
    public Picture(WrappedStream stream, PictureType? type = null, string? description = null)
    {
        (Cached, Reliance) = SharedMemoryStreamPoolInstance.Instance.AcquireFromWrappedStream(stream);
        Hash ??= Cached.Hash;
        PicType = type;
        Description = description;
    }
    public string? Description { get; init; }
    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;

    public WrappedStream? Data => Cached?.Stream;
    public PictureType? PicType { get; }
    public string? Hash { get; }

    public void Dispose()
    {
        Reliance?.Dispose();
        Reliance = null;
        Cached = null;
        GC.SuppressFinalize(this);
    }
}
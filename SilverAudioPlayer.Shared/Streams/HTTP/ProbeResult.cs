namespace SilverAudioPlayer.Shared;

public class ProbeResult
{
    public long Length { get; }
    public bool SupportsRange { get; }
    public Memory<byte>? InitialChunk { get; }

    public ProbeResult(long length, bool supportsRange, Memory<byte>? initialChunk = null)
    {
        Length = length;
        SupportsRange = supportsRange;
        InitialChunk = initialChunk;
    }
}

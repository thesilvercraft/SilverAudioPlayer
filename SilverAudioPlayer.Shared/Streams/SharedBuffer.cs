namespace SilverAudioPlayer.Shared;

/// <summary>
/// A thread-safe, centralized holder for underlying source byte arrays.
/// </summary>
public class SharedBuffer
{
    private byte[]? _bytes;
    public byte[] Bytes => _bytes ?? throw new ObjectDisposedException(nameof(SharedBuffer), "The backing data array has been released.");
    public bool IsDisposed => _bytes == null;

    public SharedBuffer(byte[] bytes)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
    }

    public void Release()
    {
        _bytes = null;
    }
}
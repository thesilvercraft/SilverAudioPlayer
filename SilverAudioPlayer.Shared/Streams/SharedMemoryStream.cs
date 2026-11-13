namespace SilverAudioPlayer.Shared;

/// <summary>
/// A high-performance, modern seekable stream that safely shares an immutable buffer context.
/// </summary>
public class SharedMemoryStream : Stream
{
    private readonly SharedBuffer _buffer;
    private long _position;
    private bool _isDisposed;

    public SharedMemoryStream(SharedBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public override bool CanRead => !_isDisposed && !_buffer.IsDisposed;
    public override bool CanSeek => !_isDisposed && !_buffer.IsDisposed;
    public override bool CanWrite => false;
    public override long Length => _buffer.Bytes.Length;

    public override long Position
    {
        get
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SharedMemoryStream));
            return _position;
        }
        set
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SharedMemoryStream));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SharedMemoryStream));
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count)
            throw new ArgumentException("Offset and length were out of bounds for the array.");

        byte[] source = _buffer.Bytes;

        long available = source.Length - _position;
        if (available <= 0 || _position > int.MaxValue) return 0;

        int toRead = (int)Math.Min(available, count);

        source.AsSpan((int)_position, toRead).CopyTo(buffer.AsSpan(offset, toRead));

        _position += toRead;
        return toRead;
    }

    public override long Seek(long offset, SeekOrigin loc)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SharedMemoryStream));

        long newPosition = loc switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin specifier.", nameof(loc))
        };

        if (newPosition < 0)
            throw new IOException("Cannot seek to a position prior to the origin boundary.");

        _position = newPosition;
        return _position;
    }

    protected override void Dispose(bool disposing)
    {
        _isDisposed = true;
        base.Dispose(disposing);
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
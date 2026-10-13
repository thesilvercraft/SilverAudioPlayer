namespace SilverAudioPlayer.Shared;

/// <summary>
/// A high-performance, modern seekable stream that safely shares an immutable buffer context.
/// </summary>
public class SharedMemoryStream : Stream
{
    private readonly SharedBuffer _buffer;
    private long _position;

    public SharedMemoryStream(SharedBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public override bool CanRead => !_buffer.IsDisposed;
    public override bool CanSeek => !_buffer.IsDisposed;
    public override bool CanWrite => false;
    public override long Length => _buffer.Bytes.Length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Safe access check; automatically throws ObjectDisposedException via wrapper if released
        byte[] source = _buffer.Bytes;

        long available = source.Length - _position;
        if (available <= 0) return 0;

        int toRead = (int)Math.Min(available, count);

        // Modern .NET Span manipulation uses SIMD/vectorized assembly instructions,
        // rendering legacy manual unrolled loops completely obsolete.
        source.AsSpan((int)_position, toRead).CopyTo(buffer.AsSpan(offset, toRead));

        _position += toRead;
        return toRead;
    }

    public override long Seek(long offset, SeekOrigin loc)
    {
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

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
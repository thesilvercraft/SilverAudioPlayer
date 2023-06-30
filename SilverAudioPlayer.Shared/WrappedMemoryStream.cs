using System.Diagnostics;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class MemoryArrayHolder
{
    public byte[] Bytes { get; set; }
}
public class FakedMemoryStream : Stream
{
    private MemoryArrayHolder MemoryArrayHolder;

    public FakedMemoryStream(MemoryArrayHolder memoryArrayHolder)
    {
        MemoryArrayHolder = memoryArrayHolder;
    }


    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        
        int n = (int)(Length - Position);
        if (n > count)
            n = count;
        if (n <= 0)
            return 0;
        if (n <= 8)
        {
            int byteCount = n;
            while (--byteCount >= 0)
                buffer[offset + byteCount] = MemoryArrayHolder.Bytes[Position+byteCount];
        }
        else
            Buffer.BlockCopy(MemoryArrayHolder.Bytes, (int)Position, buffer, offset, n);
        Position += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin loc)
    {
        switch (loc)
        {
            case SeekOrigin.Begin:
            {
                int tempPosition = unchecked((int)offset);
                if (offset < 0)
                    throw new IOException("IO_SeekBeforeBegin");
                Position = tempPosition;
                break;
            }
            case SeekOrigin.Current:
            {
                int tempPosition = (int)unchecked(Position + (int)offset);
                if (unchecked(Position + offset) < 0 || tempPosition < 0)
                    throw new IOException("IO_SeekBeforeBegin");
                Position = tempPosition;
                break;
            }
            case SeekOrigin.End:
            {
                int tempPosition = (int)unchecked(Length + (int)offset);
                if (unchecked(Length + offset) < 0 || tempPosition < 0)
                    throw new IOException("IO_SeekBeforeBegin");
                Position = tempPosition;
                break;
            }
            default:
                throw new ArgumentException("SR.Argument_InvalidSeekOrigin");
        }

        Debug.Assert(Position >= 0, "_position >= 0");
        return Position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }


    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => MemoryArrayHolder.Bytes.Length;
    public override long Position { get; set; }
}
public  class WrappedMemoryStream : WrappedStream, IDisposable
{
    public WrappedMemoryStream(byte[] data, MimeType? mt = null)
    {
        _data = new(){Bytes = data};
        if (mt == null)
        {
            var stream2 = GetStream();
            try
            {
                mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
            }
            finally
            {
                stream2.Dispose();
                Streams.Remove(stream2);
            }
        }
        MimeType = mt;
    }
    private MemoryArrayHolder _data;
    List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType { get; }
    public override Stream GetStream()
    {
        FakedMemoryStream f = new(_data);
        Streams.Add(f);
        return f;
    }

    public override bool ShouldDisposeStream => true;
 
    public override void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
        _data.Bytes = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        foreach (var stream in Streams.Where(x => x.CanRead))
            stream.Dispose();
    }

    ~WrappedMemoryStream()
    {
        Dispose(false);
    }
  
}
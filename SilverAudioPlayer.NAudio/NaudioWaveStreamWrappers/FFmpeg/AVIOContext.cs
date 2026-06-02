#if FFmpeg
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public sealed class FFmpegAVIOHelper : IDisposable
{
    public FFmpegAVIOHelper([NotNull] Stream stream, bool disposeStream = true)
    {
        unsafe
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            DisposeStream = disposeStream;
            Virtual = new AvioStreamCallbacks(Seek, Read);
        }
    }

    private bool Disposed { get; set; }

    private bool DisposeStream { get; }

    [NotNull] private Stream Stream { get; }

    public AvioStreamCallbacks Virtual { get; }

    public void Dispose()
    {
        if (Disposed)
            return;

        if (DisposeStream)
            Stream.Dispose();

        Disposed = true;
    }


    private unsafe long Seek(void* stream, long offset, int whence)
    {
        //Debug.WriteLine($"seek {offset} to {whence}");
        var force = whence & ffmpeg.AVSEEK_FORCE;
        whence &= ~ffmpeg.AVSEEK_FORCE;
        if ((Whence)whence == Whence.AVSEEK_SIZE) return Stream.Length;
        if ((Whence)whence != Whence.Current && (Whence)whence != Whence.Set && (Whence)whence != Whence.End)
            return ffmpeg.AVERROR(ffmpeg.EINVAL);
        Stream.Seek(offset, (Whence)whence switch
        {
            Whence.Current => SeekOrigin.Current,
            Whence.End => SeekOrigin.End,
            Whence.Set => SeekOrigin.Begin,
            _ => throw new NotImplementedException()
        });

        return 0;
    }

    private unsafe int Read(void* stream, byte* destination, int count)
    {
        var buffer = new byte[count];
        var read = Stream.Read(buffer, 0, buffer.Length);
        if (read == 0 && Stream.Position == Stream.Length) return ffmpeg.AVERROR_EOF;
        Marshal.Copy(buffer, 0, (IntPtr)destination, read);
        //Debug.WriteLine($"read {read}/{count} ");
        return read;
    }
}

//#define SEEK_SET	0	/* Seek from beginning of file.  */
//#define SEEK_CUR	1	/* Seek from current position.  */
//#define SEEK_END	2	/* Seek from end of file.  */\
public enum Whence
{
    Set = 0,
    Current = 1,
    End = 2,
    AVSEEK_SIZE = ffmpeg.AVSEEK_SIZE
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int* VirtualSeek(IntPtr opaque, nint offset, Whence whence);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int* VirtualRead(IntPtr opaque, IntPtr destination, nuint bytes);

public struct AvioStreamCallbacks
{
    [NotNull] [MarshalAs(UnmanagedType.FunctionPtr)]
    public readonly avio_alloc_context_read_packet Read;

    [NotNull] [MarshalAs(UnmanagedType.FunctionPtr)]
    public readonly avio_alloc_context_seek Seek;

    public AvioStreamCallbacks(
        [NotNull] avio_alloc_context_seek seek,
        [NotNull] avio_alloc_context_read_packet read
    )
    {
        Seek = seek;
        Read = read;
    }

    private readonly bool Equals(AvioStreamCallbacks other)
    {
        return Seek.Equals(other.Seek) && Read.Equals(other.Read);
    }

    public readonly override bool Equals(object? obj)
    {
        return obj is AvioStreamCallbacks @virtual && Equals(@virtual);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Seek, Read);
    }

    public static bool operator ==(AvioStreamCallbacks left, AvioStreamCallbacks right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AvioStreamCallbacks left, AvioStreamCallbacks right)
    {
        return !left.Equals(right);
    }
}
#endif

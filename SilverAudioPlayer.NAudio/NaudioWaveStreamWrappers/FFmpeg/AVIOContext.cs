#if FFmpeg
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public sealed class FFmpegAVIOHelper : IDisposable
{

    [SuppressMessage("SonarLint", "S6640:Avoid using unsafe code blocks", Justification = "Interop delegate requires raw pointers")]
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

    [SuppressMessage("SonarLint", "S6640:Avoid using unsafe code blocks", Justification = "Interop requires raw pointers")]
    private unsafe long Seek(void* stream, long offset, int whence)
    {
        if (whence == ffmpeg.AVSEEK_SIZE) 
            return Stream.Length;
        whence &= ~ffmpeg.AVSEEK_FORCE;
        var origin = (Whence)whence switch
        {
            Whence.Set => SeekOrigin.Begin,
            Whence.Current => SeekOrigin.Current,
            Whence.End => SeekOrigin.End,
            _ => throw new ArgumentOutOfRangeException(nameof(whence), "Invalid seek origin")
        };
        return Stream.Seek(offset, origin);
    }
    [SuppressMessage("SonarLint", "S6640:Avoid using unsafe code blocks", Justification = "Interop requires raw pointers")]
    private unsafe int Read(void* stream, byte* destination, int count)
    {
        var buffer = new byte[count];
        var read = Stream.Read(buffer, 0, buffer.Length);
        if (read == 0 && Stream.Position == Stream.Length) return ffmpeg.AVERROR_EOF;
        Marshal.Copy(buffer, 0, (IntPtr)destination, read);
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
[SuppressMessage("SonarLint", "S6640:Avoid using unsafe code blocks", Justification = "Interop delegate requires raw pointers")]
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int* VirtualSeek(IntPtr opaque, nint offset, Whence whence);

[SuppressMessage("SonarLint", "S6640:Avoid using unsafe code blocks", Justification = "Interop delegate requires raw pointers")]
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

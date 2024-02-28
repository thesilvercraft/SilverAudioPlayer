using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OpenMPT;


public sealed class MPTVirtualStreamHelper : IDisposable
{
    public MPTVirtualStreamHelper([NotNull] Stream stream, bool disposeStream = true)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        DisposeStream = disposeStream;
        Virtual = new openmpt_stream_callbacks(Seek, Read, Tell);
    }

    private bool Disposed { get; set; }

    private bool DisposeStream { get; }

    [NotNull]
    private Stream Stream { get; }

    public openmpt_stream_callbacks Virtual { get; }

    public void Dispose()
    {
        if (Disposed)
            return;

        if (DisposeStream)
            Stream.Dispose();

        Disposed = true;
    }



    private int Seek(IntPtr stream, nint offset, Whence whence)
    {
        return (int)Stream.Seek(offset, whence switch
        {
            Whence.Current => SeekOrigin.Current,
            Whence.End => SeekOrigin.End,
            Whence.Set => SeekOrigin.Begin,
            _ =>throw new NotImplementedException()
        });
    }
    private nuint Read(IntPtr stream, IntPtr destination, nuint count)
    {
        var buffer = new byte[count];
        var read = Stream.Read(buffer, 0, buffer.Length);
        Marshal.Copy(buffer, 0, destination, read);
        return (nuint)read;
    }



    private nint Tell(IntPtr userData)
    {
        return (nint)Stream.Position;
    }
}
public enum Whence : int{
    Set = NativeMethods.OPENMPT_STREAM_SEEK_SET,
    Current = NativeMethods.OPENMPT_STREAM_SEEK_CUR,
    End = NativeMethods.OPENMPT_STREAM_SEEK_END,
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate int VirtualSeek(IntPtr stream, nint offset, Whence whence);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [NativeTypeName("openmpt_stream_read_func")]

public delegate nuint VirtualRead(IntPtr stream, IntPtr destination, nuint bytes);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate nint VirtualTell(IntPtr userData);
    public struct openmpt_stream_callbacks
    {
        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly VirtualRead Read;
        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly VirtualSeek Seek;
        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly VirtualTell Tell;

        public openmpt_stream_callbacks(
            [NotNull] VirtualSeek seek,
            [NotNull] VirtualRead read,
            [NotNull] VirtualTell tell)
        {
            Seek = seek ?? throw new ArgumentNullException(nameof(seek));
            Read = read ?? throw new ArgumentNullException(nameof(read));
            Tell = tell ?? throw new ArgumentNullException(nameof(tell));
        }

        private readonly bool Equals(openmpt_stream_callbacks other)
        {
            return  Seek.Equals(other.Seek) && Read.Equals(other.Read) &&
                   Tell.Equals(other.Tell);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is openmpt_stream_callbacks @virtual && Equals(@virtual);
        }

        public override int GetHashCode()
    {
        return HashCode.Combine(Seek, Read, Tell);
    }

    public static bool operator ==(openmpt_stream_callbacks left, openmpt_stream_callbacks right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(openmpt_stream_callbacks left, openmpt_stream_callbacks right)
        {
            return !left.Equals(right);
        }

    }
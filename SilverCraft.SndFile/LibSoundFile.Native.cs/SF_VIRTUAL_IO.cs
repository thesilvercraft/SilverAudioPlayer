using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace sndfile;
public sealed class SfVirtualStreamHelper : IDisposable
{
    public SfVirtualStreamHelper([NotNull] Stream stream, bool disposeStream = true)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        DisposeStream = disposeStream;
        Virtual = new SF_VIRTUAL_IO(Length, Seek, Read, Write, Tell);
    }

    private bool Disposed { get; set; }

    private bool DisposeStream { get; }

    [NotNull]
    private Stream Stream { get; }

    public SF_VIRTUAL_IO Virtual { get; }

    public void Dispose()
    {
        if (Disposed)
            return;

        if (DisposeStream)
            Stream.Dispose();

        Disposed = true;
    }

    private long Length(IntPtr userData)
    {
        return Stream.Length;
    }

    private long Seek(long offset, Whence seek, IntPtr userData)
    {
        return Stream.Seek(offset, seek switch
        {
            Whence.Current=> SeekOrigin.Current,
            Whence.End => SeekOrigin.End,
            Whence.Set => SeekOrigin.Begin
        });
    }

    private long Read(IntPtr ptr, long count, IntPtr userData)
    {
        var buffer = new byte[count];
        var read = Stream.Read(buffer, 0, buffer.Length);
        Marshal.Copy(buffer, 0, ptr, read);
        return read;
    }

    private long Write(IntPtr ptr, long count, IntPtr userData)
    {
        if (!Stream.CanWrite) return 0;
        var buffer = new byte[count];
        Marshal.Copy(ptr, buffer, 0, buffer.Length);
        Stream.Write(buffer, 0, buffer.Length);
        return count;
    }

    private long Tell(IntPtr userData)
    {
        return Stream.Position;
    }
}
    public struct SF_VIRTUAL_IO
    {
        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly SfVirtualLength Length;

        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly SfVirtualSeek Seek;

        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly SfVirtualRead Read;

        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly SfVirtualWrite Write;

        [NotNull]
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly SfVirtualTell Tell;

        public SF_VIRTUAL_IO(
            [NotNull] SfVirtualLength length,
            [NotNull] SfVirtualSeek seek,
            [NotNull] SfVirtualRead read,
            [NotNull] SfVirtualWrite write,
            [NotNull] SfVirtualTell tell)
        {
            Length = length ?? throw new ArgumentNullException(nameof(length));
            Seek = seek ?? throw new ArgumentNullException(nameof(seek));
            Read = read ?? throw new ArgumentNullException(nameof(read));
            Write = write ?? throw new ArgumentNullException(nameof(write));
            Tell = tell ?? throw new ArgumentNullException(nameof(tell));
        }

        private bool Equals(SF_VIRTUAL_IO other)
        {
            return Length.Equals(other.Length) && Seek.Equals(other.Seek) && Read.Equals(other.Read) &&
                   Write.Equals(other.Write) && Tell.Equals(other.Tell);
        }

        public override bool Equals(object? obj)
        {
            return obj is SF_VIRTUAL_IO @virtual && Equals(@virtual);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Length.GetHashCode();
                hashCode = (hashCode * 397) ^ Seek.GetHashCode();
                hashCode = (hashCode * 397) ^ Read.GetHashCode();
                hashCode = (hashCode * 397) ^ Write.GetHashCode();
                hashCode = (hashCode * 397) ^ Tell.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(SF_VIRTUAL_IO left, SF_VIRTUAL_IO right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SF_VIRTUAL_IO left, SF_VIRTUAL_IO right)
        {
            return !left.Equals(right);
        }

    }
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate long SfVirtualLength(IntPtr userData);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate long SfVirtualSeek(long offset, Whence seek, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate long SfVirtualRead(IntPtr ptr, long count, IntPtr userData);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate long SfVirtualWrite(IntPtr ptr, long count, IntPtr userData);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate long SfVirtualTell(IntPtr userData);
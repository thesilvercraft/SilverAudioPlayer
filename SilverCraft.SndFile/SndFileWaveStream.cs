using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore;
using sndfile;

namespace SilverCraft.SndFile;

public class SndFileWaveStream : ISampleSource
{
    private unsafe sf_private_tag* infile;
    private SF_VIRTUAL_IO IO;
    SfVirtualStreamHelper Helper;
    SF_INFO info;
    public static bool ResolverIsSet = false;
    public unsafe SndFileWaveStream(Stream s)
    {
        Helper = new SfVirtualStreamHelper(s);
        IO = Helper.Virtual;
        if (!ResolverIsSet)
        {
            ResolverIsSet = true;
            NativeLibrary.SetDllImportResolver(typeof(Methods).Assembly, Methods.DllImportResolver);
        }

        info = new SF_INFO();
        infile = Methods.sf_open_virtual(ref IO, Mode.Read, ref info, 0);
        if (infile == null)
        {
            Helper.Dispose();
            throw new Exception($"SndFile {Marshal.PtrToStringAnsi((IntPtr)Methods.sf_strerror(null))}");
        }
        WaveFormat = new WaveFormat(info.samplerate, 32, info.channels);
        Length = info.frames * info.channels;
        Debug.WriteLine($"Length {Length}");
        Debug.WriteLine($"Sample rate: {info.samplerate}Hz");
        Debug.WriteLine($"Channel count: {info.channels}");
        Debug.WriteLine($"Format: {info.format}");
        Debug.WriteLine($"Frames: {info.frames}");
        Debug.WriteLine($"Seekable: {info.seekable}");
    }
    public unsafe SndFileWaveStream(ref sf_private_tag* f, SF_INFO info)
    {
        infile = f;
        WaveFormat = new WaveFormat(info.samplerate, 16, info.channels);
    }
    public unsafe int Read(float[] buffer, int offset, int count)
    {
        fixed (float* f = buffer)
        {
            return (int)Methods.sf_read_float(infile, f, count);
        }
    }
    public unsafe void Dispose()
    {
      
        Debug.WriteLine("Disposing SndFileWaveStream!");
#if DEBUG
        var st = new StackTrace(true);
        for(int i =0; i< st.FrameCount; i++ )
        {
            // Note that high up the call stack, there is only
            // one stack frame.
            var sf = st.GetFrame(i);
            Debug.WriteLine($"{sf.GetMethod()} { sf.GetFileLineNumber()}");
        }
#endif
        _ = Methods.sf_close(infile);
        Debug.WriteLine("Disposing SfVirtualStreamHelper");
        Helper.Dispose();
    }

    public bool CanSeek => info.seekable != 0;
    public WaveFormat WaveFormat { get; set; }
    public unsafe long Position
    {
        get => Methods.sf_seek(infile, 0, Whence.Current) * info.channels;
        set
        {
            if (info.seekable != 0)
            {
                Methods.sf_seek(infile, (nint)value / info.channels, Whence.Set);
            }
        }
    }

    public long Length { get; }
}
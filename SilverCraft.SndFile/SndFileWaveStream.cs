using System.Diagnostics;
using CSCore;
using sndfile;

namespace SilverCraft.SndFile;

public class SndFileWaveStream : ISampleSource
{
    private unsafe sf_private_tag* infile;
    private SF_VIRTUAL_IO IO;
    SF_INFO info = new SF_INFO();

    public unsafe SndFileWaveStream(Stream s)
    {
        SfVirtualStreamHelper helper = new(s);
        IO= helper.Virtual;
        infile = Methods.sf_open_virtual(ref IO, Mode.Read, ref info,  0);
        WaveFormat = new WaveFormat(info.samplerate, 32, info.channels);
        Length = info.frames/info.samplerate;
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
    public int Read(float[] buffer, int offset, int count)
    {
        unsafe
        {
            fixed (float* f = buffer)
            {
                return (int)Methods.sf_read_float(infile, f, count);
            }
        }
    }
   

    public void Dispose()
    {
        unsafe
        {
            Debug.WriteLine("DISPOSE!!!!");
           _ = Methods.sf_close(infile);
        }
    }

    public bool CanSeek => false;
    public WaveFormat WaveFormat { get; set; }
    public long Position
    {
        get
        {
            return 0;
        }
        set
        {
            if (info.seekable!=0)
            {
             //   Debug.WriteLine(value);
                unsafe
                {
                    //Methods.sf_seek(infile, (nint)(value*info.samplerate), Whence.Set);
                }
            }
          
        }
    }

    public long Length { get; }
}
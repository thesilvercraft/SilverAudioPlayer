using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.NAudio;
using SilverCraft.CSCore;
using SilverCraft.CSCore.OpenMPT;
using SilverMagicBytes;
using DynamicallyLoadedBindings = FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings;
using WaveFormat = SilverCraft.CSCore.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class FFmpeg : INaudioWaveStreamWrapper
{
    public FFmpeg()
    {
        if (!SupportedOnPlatform)
        {
            Log.Error("ffmpeg not found, not supporting ffmpeg playback");
        }
        else
        {
            unsafe
            {
                Console.WriteLine("Current directory: " + Environment.CurrentDirectory);
                Console.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
                DynamicallyLoadedBindings.LibrariesPath = "/usr/lib/";
                ffmpeg.RootPath = "/usr/lib/";
                DynamicallyLoadedBindings.Initialize();
                Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
                ffmpeg.av_log_set_level(ffmpeg.AV_LOG_VERBOSE);

                av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
                {
                    unsafe
                    {
                        if (level > ffmpeg.av_log_get_level()) return;

                        var lineSize = 1024;
                        var lineBuffer = stackalloc byte[lineSize];
                        var printPrefix = 1;
                        ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                        var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
                        Debug.WriteLine(line);
                        Log.Information(line);
                    }
                };
                ffmpeg.av_log_set_callback(logCallback);
            }
        }
    }
    //https://wiki.openmpt.org/Manual:_Module_formats
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.MKVMime];
    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(x));
    static readonly string[] DllLoc = ["/usr/lib/libavcodec.so",
    ];

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new FFMpegWaveStream(stream));

}
public class FFMpegWaveStream : IWaveSource
{
    private AudioStreamDecoder helper;
    public  FFMpegWaveStream(Stream s)
    {
        helper = new(s);
        Console.WriteLine(helper.SampleFormat);
        WaveFormat = new WaveFormat(helper.SampleRate, 32, helper.NumberOfChannels, AudioEncoding.IeeeFloat);
    }

    private byte[] overflow=new byte[ 0];
    private int valuesleftinoverflow= 0;
    private int overflowstart = 0;

    public  int Read(byte[] buffer, int offset, int count)
    {
        var ogcount = count;
        while(count > 0 )
        {
            if (valuesleftinoverflow > 0)
            {
                var min=Math.Min(valuesleftinoverflow, count);
                Array.Copy(overflow, overflowstart, buffer, offset, min);
                overflowstart += min;
                if (min == count)
                {
                    valuesleftinoverflow -=count;
                    return ogcount;
                }
                count -= valuesleftinoverflow;
                offset += valuesleftinoverflow;
                valuesleftinoverflow =0;
            }
            if(valuesleftinoverflow ==0 )
            {
                if (helper.TryDecodeNextFrame(out var frame))
                {
                    var min = frame.nb_samples * helper.NumberOfChannels * helper.BytesPerSample;
                    if (overflow.Length < min)
                    {
                        overflow = new byte[min];
                    }
                    for (int i = 0; i < frame.nb_samples; i++)
                    {
                        for (uint channel = 0; channel < helper.NumberOfChannels; channel++)
                        {
                            unsafe
                            {
                                var data = frame.data[channel] + (helper.BytesPerSample * i);
                                overflow[valuesleftinoverflow] = *data;
                                overflow[valuesleftinoverflow + 1] = *(data + 1);
                                overflow[valuesleftinoverflow + 2] = *(data + 2);
                                overflow[valuesleftinoverflow + 3] = *(data + 3);
                                valuesleftinoverflow += helper.BytesPerSample;
                            }
                        }
                    }
                    overflowstart = 0;
                }
                else
                {
                    return ogcount - count;
                }
            }
        }
        return ogcount;
    }
    private bool isDisposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual unsafe void Dispose(bool disposing)
    {
        if (isDisposed) return;
        if (disposing)
        {
            helper.Dispose();

        }

        isDisposed = true;
    }


    ~FFMpegWaveStream()
    {
        Dispose(false);
    }


    public bool CanSeek => true;
    public WaveFormat WaveFormat { get;  }
    public unsafe long Position
    {
        get => helper.GetPosition();
        set
        {
             helper.Seek((long)((double)value/WaveFormat.BytesPerSecond*1000000));
        }
    }

    public long Length { get=>(long)(helper.GetDuration()*WaveFormat.BytesPerSecond); }
}
#if FFmpeg
using FFmpeg.AutoGen;
using SilverCraft.CSCore;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public class FFMpegWaveStream : IWaveSource
{
    private AudioStreamDecoder helper;
    public  FFMpegWaveStream(Stream s)
    {
        helper = new(s);
        Console.WriteLine(helper.SampleFormat);
       
        WaveFormat = new WaveFormat(helper.SampleRate, helper.SampleFormat switch
        {
            AVSampleFormat.AV_SAMPLE_FMT_U8 or  AVSampleFormat.AV_SAMPLE_FMT_U8P => 8,
            AVSampleFormat.AV_SAMPLE_FMT_S16 or AVSampleFormat.AV_SAMPLE_FMT_S16P  => 16,
            AVSampleFormat.AV_SAMPLE_FMT_S32 or  AVSampleFormat.AV_SAMPLE_FMT_S32P => 32,
            AVSampleFormat.AV_SAMPLE_FMT_FLT or    AVSampleFormat.AV_SAMPLE_FMT_FLTP  => 32,
            AVSampleFormat.AV_SAMPLE_FMT_DBL or  AVSampleFormat.AV_SAMPLE_FMT_DBLP=> 64,
            AVSampleFormat.AV_SAMPLE_FMT_S64 or    AVSampleFormat.AV_SAMPLE_FMT_S64P => 64,
            _ => throw new ArgumentOutOfRangeException()
        }, helper.NumberOfChannels, helper.SampleFormat switch
        {
            AVSampleFormat.AV_SAMPLE_FMT_U8 or  AVSampleFormat.AV_SAMPLE_FMT_U8P => AudioEncoding.Pcm,
            AVSampleFormat.AV_SAMPLE_FMT_S16 or AVSampleFormat.AV_SAMPLE_FMT_S16P  => AudioEncoding.Pcm,
            AVSampleFormat.AV_SAMPLE_FMT_S32 or  AVSampleFormat.AV_SAMPLE_FMT_S32P => AudioEncoding.Pcm,
            AVSampleFormat.AV_SAMPLE_FMT_FLT or    AVSampleFormat.AV_SAMPLE_FMT_FLTP  => AudioEncoding.IeeeFloat,
            AVSampleFormat.AV_SAMPLE_FMT_DBL or  AVSampleFormat.AV_SAMPLE_FMT_DBLP=> AudioEncoding.IeeeFloat,
            AVSampleFormat.AV_SAMPLE_FMT_S64 or    AVSampleFormat.AV_SAMPLE_FMT_S64P => AudioEncoding.Pcm,
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    private byte[] overflow=[];
    private int valuesleftinoverflow= 0;
    private int overflowstart = 0;

    public int Read(byte[] buffer, int offset, int count)
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

            if (valuesleftinoverflow != 0) continue;
            {
                if (helper.TryDecodeNextFrame(out var frame))
                {
                    var min = frame.nb_samples * helper.NumberOfChannels * helper.BytesPerSample;
                    if (overflow.Length < min+valuesleftinoverflow)
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

    protected virtual  void Dispose(bool disposing)
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
    public long Position
    {
        get => (long)(helper.GetPosition()*WaveFormat.BytesPerSecond);
        set
        {
            helper.Seek((long)((double)value/WaveFormat.BytesPerSecond*1000000));
        }
    }

    public long Length { get=>(long)(helper.GetDuration()*WaveFormat.BytesPerSecond); }
}
#endif

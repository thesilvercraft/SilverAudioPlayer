#if CSCore
#if FFmpeg
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;
internal static class FFmpegHelper
{
    public static unsafe string av_strerror(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
        return message;
    }

    public static int ThrowExceptionIfError(this int error)
    {
        if (error < 0) throw new ApplicationException(av_strerror(error));
        return error;
    }
}
public  unsafe class AudioStreamDecoder : IDisposable
{
    private readonly AVCodecContext* _pCodecContext;
    private readonly AVFormatContext* _pFormatContext;
    private readonly AVFrame* _pFrame;
    private readonly AVPacket* _pPacket;
    private readonly AVFrame* _receivedFrame;
    private readonly int _streamIndex;
    private void* buffer;
    private FFmpegAVIOHelper stuff;
    private AVIOContext* iocontext;
    public AudioStreamDecoder(Stream stream)
    {
        _pFormatContext = ffmpeg.avformat_alloc_context();
        _receivedFrame = ffmpeg.av_frame_alloc();
        stuff=new(stream);
        buffer = ffmpeg.av_malloc(8192);
         iocontext = ffmpeg.avio_alloc_context((byte*)buffer, 8192, 0, (void*)1, stuff.Virtual.Read, null, stuff.Virtual.Seek);
        var pFormatContext = _pFormatContext;
        pFormatContext->pb = iocontext;
        pFormatContext->flags = ffmpeg.AVFMT_FLAG_CUSTOM_IO;
        ffmpeg.avformat_open_input(&pFormatContext, "", null, null).ThrowExceptionIfError();
        ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();
        AVCodec* codec = null;
        _streamIndex = ffmpeg
            .av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0)
            .ThrowExceptionIfError();
        _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

  

        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamIndex]->codecpar)
            .ThrowExceptionIfError();
        ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();
        NumberOfChannels = _pCodecContext->ch_layout.nb_channels;
      
        BytesPerSample= ffmpeg.av_get_bytes_per_sample(_pCodecContext->sample_fmt);
        CodecName = ffmpeg.avcodec_get_name(codec->id);
        SampleRate = _pCodecContext->sample_rate;
        SampleFormat = _pCodecContext->sample_fmt;

        _pPacket = ffmpeg.av_packet_alloc();
        _pFrame = ffmpeg.av_frame_alloc();
    }

    public string CodecName { get; }
    public AVSampleFormat SampleFormat { get; }
    public int NumberOfChannels { get; }
    public int SampleRate { get; }
    public long EstPos { get; private set; } = 0;
    public int BytesPerSample { get; }

    public void Dispose()
    {
        var pFrame = _pFrame;
        ffmpeg.av_frame_free(&pFrame);

        var pPacket = _pPacket;
        ffmpeg.av_packet_free(&pPacket);
        var pCodecContext = _pCodecContext;
        ffmpeg.avcodec_free_context(&pCodecContext);
        var pFormatContext = _pFormatContext;
        ffmpeg.avformat_close_input(&pFormatContext);
        ffmpeg.av_free(buffer);
        stuff.Dispose();
    }
 
    public double GetDuration()
    {
        return _pFormatContext->duration / 1000000d;  
    }

    public void Seek(long o)
    {
        ffmpeg.av_seek_frame(_pFormatContext, -1, o, ffmpeg.AVSEEK_FLAG_ANY);
    }

    public long GetPosition()
    {
        return EstPos/1000; 
    }

    public bool TryDecodeNextFrame(out AVFrame frame)
    {
        ffmpeg.av_frame_unref(_pFrame);
        ffmpeg.av_frame_unref(_receivedFrame);
        int error;

        do
        {
            try
            {
                do
                {
                    ffmpeg.av_packet_unref(_pPacket);
                    error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);

                    if (error == ffmpeg.AVERROR_EOF)
                    {
                        frame = *_pFrame;
                        EstPos = frame.best_effort_timestamp;

                        return false;
                    }
                    
                    error.ThrowExceptionIfError();
                } while (_pPacket->stream_index != _streamIndex);

                ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
            }
            finally
            {
                ffmpeg.av_packet_unref(_pPacket);
            }

            error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
        } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

        error.ThrowExceptionIfError();

       
            frame = *_pFrame;
            EstPos = frame.best_effort_timestamp;
        return true;
    }

    public IReadOnlyDictionary<string, string> GetContextInfo()
    {
        AVDictionaryEntry* tag = null;
        var result = new Dictionary<string, string>();

        while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
        {
            var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
            var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
            result.Add(key, value);
        }

        return result;
    }
}
#endif
#endif

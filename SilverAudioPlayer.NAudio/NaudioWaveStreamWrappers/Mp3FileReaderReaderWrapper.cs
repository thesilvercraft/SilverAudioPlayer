using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using NLayer.NAudioSupport;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class Mp3FileReaderReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == "audio/mpeg" || stream.MimeType == "audio/mp3" || stream.MimeType == "audio/x-mp3" || stream.MimeType == "audio/x-mpeg" || stream.MimeType == ".mp3";
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(wf => new Mp3FrameDecompressor(wf));
            return new Mp3FileReaderBase(stream.GetStream(), builder);
        }
    }
}
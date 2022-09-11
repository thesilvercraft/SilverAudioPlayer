using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using NLayer.NAudioSupport;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class Mp3FileReaderReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == KnownMimes.MP3Mime;
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(wf => new Mp3FrameDecompressor(wf));
            return new Mp3FileReaderBase(stream.GetStream(), builder);
        }
    }
}
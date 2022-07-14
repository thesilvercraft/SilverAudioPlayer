using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == "audio/wave" || stream.MimeType == "audio/wav" || stream.MimeType == "audio/x-wav" || stream.MimeType == ".wav";
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new WaveFileReader(stream.GetStream());
        }
    }
}
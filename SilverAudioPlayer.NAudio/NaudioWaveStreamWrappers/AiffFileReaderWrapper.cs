using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == "audio/aiff" || stream.MimeType == "audio/x-aiff" || stream.MimeType == ".aiff" || stream.MimeType == ".aif";
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new AiffFileReader(stream.GetStream());
        }
    }
}
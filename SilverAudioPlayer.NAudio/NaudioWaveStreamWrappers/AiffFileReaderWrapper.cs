using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == KnownMimes.AiffMime;
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new AiffFileReader(stream.GetStream());
        }
    }
}
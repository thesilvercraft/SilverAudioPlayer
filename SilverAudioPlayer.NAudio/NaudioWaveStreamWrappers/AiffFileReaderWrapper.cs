using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public byte GetPlayingAbility(WrappedStream stream)
        {
            if (stream.MimeType == KnownMimes.AiffMime)
            {
                return 40;
            }
            return 0;
        }
     
        public WaveStream GetStream(WrappedStream stream)
        {
            return new AiffFileReader(stream.GetStream());
        }
    }
}
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == KnownMimes.WAVMime;
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new WaveFileReader(stream.GetStream());
        }
    }
}
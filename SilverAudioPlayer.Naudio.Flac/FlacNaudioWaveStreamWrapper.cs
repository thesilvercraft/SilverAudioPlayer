using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.Naudio.Flac
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
    {

        public byte GetPlayingAbility(WrappedStream stream)
        {
            if (stream.MimeType == KnownMimes.FLACMime)
            {
                return 40;
            }
            return 0;
        }


        public WaveStream GetStream(WrappedStream stream)
        {
            return new FlacReader(stream.GetStream());
        }
    }
}
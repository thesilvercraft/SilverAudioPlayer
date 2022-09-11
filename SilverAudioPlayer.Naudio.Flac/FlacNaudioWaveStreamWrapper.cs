using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.Naudio.Flac
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == KnownMimes.FLACMime;
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new FlacReader(stream.GetStream());
        }
    }
}
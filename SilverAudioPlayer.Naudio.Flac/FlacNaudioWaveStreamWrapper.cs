using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.Naudio.Flac
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(WrappedStream stream)
        {
            return stream.MimeType == "audio/flac" || stream.MimeType == "audio/x-flac" || stream.MimeType == ".flac";
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            return new FlacReader(stream.GetStream());
        }
    }
}
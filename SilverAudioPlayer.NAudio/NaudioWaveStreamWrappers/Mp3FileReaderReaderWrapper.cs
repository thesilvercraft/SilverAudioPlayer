using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

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
            return new Mp3FileReader(stream.RegenStream());
        }
    }
}
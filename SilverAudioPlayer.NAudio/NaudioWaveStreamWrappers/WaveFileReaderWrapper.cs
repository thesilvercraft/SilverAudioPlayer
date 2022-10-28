using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType> { KnownMimes.WAVMime };

    public byte GetPlayingAbility(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.WAVMime) return 50;
        return 0;
    }

    public WaveStream GetStream(WrappedStream stream)
    {
        return new WaveFileReader(stream.GetStream());
    }
}
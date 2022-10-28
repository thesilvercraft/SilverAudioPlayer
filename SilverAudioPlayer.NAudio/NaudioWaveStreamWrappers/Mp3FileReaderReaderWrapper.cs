using System.Composition;
using NAudio.Wave;
using NLayer.NAudioSupport;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class Mp3FileReaderReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType> { KnownMimes.MP3Mime };

    public byte GetPlayingAbility(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.MP3Mime) return 40;
        return 0;
    }

    public WaveStream GetStream(WrappedStream stream)
    {
        var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(wf => new Mp3FrameDecompressor(wf));
        return new Mp3FileReaderBase(stream.GetStream(), builder);
    }
}
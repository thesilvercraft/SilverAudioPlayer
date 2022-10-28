using System.Composition;
using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.Naudio.Flac;

[Export(typeof(INaudioWaveStreamWrapper))]
public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType> { KnownMimes.FLACMime };

    public byte GetPlayingAbility(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.FLACMime) return 40;
        return 0;
    }

    public WaveStream GetStream(WrappedStream stream)
    {
        return new FlacReader(stream.GetStream());
    }
}
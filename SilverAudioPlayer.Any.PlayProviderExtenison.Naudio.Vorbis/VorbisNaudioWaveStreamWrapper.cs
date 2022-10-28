using System.Composition;
using NAudio.Vorbis;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProviderExtenison.Naudio.Vorbis;

[Export(typeof(INaudioWaveStreamWrapper))]
public class VorbisNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType> { KnownMimes.OGGMime };

    public byte GetPlayingAbility(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.OGGMime) return 30;
        return 0;
    }

    public WaveStream GetStream(WrappedStream stream)
    {
        return new VorbisWaveReader(stream.GetStream());
    }
}
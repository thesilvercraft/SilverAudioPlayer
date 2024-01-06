using System.Composition;
using NAudio.Vorbis;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class VorbisNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = new List<MimeType> { KnownMimes.OGGMime };
    public bool SupportedOnPlatform => true;
    public WaveStream GetStream(Stream stream) => new VorbisWaveReader(stream);
}
#if CSCore
using System.Composition;
using CSCore.Codecs.FLAC;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = new List<MimeType> { KnownMimes.FLACMime };

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new FlacFile(stream));
}
#endif
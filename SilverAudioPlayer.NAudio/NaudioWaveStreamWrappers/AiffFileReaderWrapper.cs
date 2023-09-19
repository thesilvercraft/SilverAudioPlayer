using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = new List<MimeType> { KnownMimes.AiffMime };
    public WaveStream GetStream(Stream stream) => new AiffFileReader(stream);
}
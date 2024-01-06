using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = new List<MimeType> { KnownMimes.WAVMime };
    public bool SupportedOnPlatform => true;
    public WaveStream GetStream(Stream stream) => new WaveFileReader(stream);
}

using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class WaveFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = [KnownMimes.WAVMime];
    public bool SupportedOnPlatform => true;
    public IWaveProvider GetStream(Stream stream) => new WaveFileReader(stream);
}

using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class NAudioAiffFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = [KnownMimes.AiffMime];

    public bool SupportedOnPlatform => true;

    public WaveStream GetStream(Stream stream) => new AiffFileReader(stream);

}
#if CSCore
[Export(typeof(INaudioWaveStreamWrapper))]
public class CSCoreAiffFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = [KnownMimes.AiffMime];
    public bool SupportedOnPlatform => true;

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new SilverCraft.CSCore.Codecs.AIFF.AiffReader(stream));

}
#endif
#if CSCore
using System.Composition;
using SilverCraft.CSCore.Codecs.FLAC;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;
[Export(typeof(INaudioWaveStreamWrapper))]
public class FlacNAudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.FLACMime];
    public bool SupportedOnPlatform => true;
    public IWaveProvider GetStream(Stream stream) => new CSCoreReader(new FlacFile(stream));
}
#endif
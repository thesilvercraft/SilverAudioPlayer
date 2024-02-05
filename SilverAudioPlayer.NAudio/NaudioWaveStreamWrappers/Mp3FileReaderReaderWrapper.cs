using System.Composition;
using NAudio.Wave;
using NLayer.NAudioSupport;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class Mp3FileReaderReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.MP3Mime];
    public bool SupportedOnPlatform => true;
    readonly Mp3FileReaderBase.FrameDecompressorBuilder builder = new(wf => new Mp3FrameDecompressor(wf));
    public WaveStream GetStream(Stream stream) => new Mp3FileReaderBase(stream, builder);
}
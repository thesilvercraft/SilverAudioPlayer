using System.Composition;
using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; set; } = new  List<MimeType> { KnownMimes.FLACMime };

    public WaveStream GetStream(Stream stream) => new FlacReader(stream);
}
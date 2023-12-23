using System.Composition;
using CSCore;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverCraft.SndFile;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class LibSndFile : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = new List<MimeType> { KnownMimes.WAVMime, KnownMimes.FLACMime, KnownMimes.AiffMime,  KnownMimes.OGGMime, KnownMimes.MP3Mime   };

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new SndFileWaveStream(stream).ToWaveSource());

}

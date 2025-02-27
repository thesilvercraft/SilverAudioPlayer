#if CSCore
using System.Composition;
using SilverCraft.CSCore;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.NAudio;
using SilverCraft.CSCore.SndFile;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class LibSndFile : INaudioWaveStreamWrapper
{
    public LibSndFile()
    {
        if (!SupportedOnPlatform)
        {
            Log.Error("LIBSNDFILE not found, not supporting libsndfile playback");
        }
    }
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.WAVMime, KnownMimes.FLACMime, KnownMimes.AiffMime, KnownMimes.OGGMime]; //, KnownMimes.MP3Mime //Libmpeg123 keeps complaining about my http stream being not nice

    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(x));
    static readonly string[] DllLoc = ["libsndfile.so","/usr/lib/x86_64-linux-gnu/libsndfile.so",
    $"{AppContext.BaseDirectory}sndfile.dll","/usr/lib/x86_64-linux-gnu/libsndfile.so.1","/usr/lib/x86_64-linux-gnu/libsndfile.so.1.0.37",
    "/usr/lib/i386-linux-gnu/libsndfile.so.1","/usr/lib/i386-linux-gnu/libsndfile.so.1.0.37",
    "/usr/lib/aarch64-linux-gnu/libsndfile.so.1","/usr/lib/aarch64-linux-gnu/libsndfile.so.1.0.28", "/usr/lib/libsndfile.so", "/usr/lib/libsndfile.so.1","/usr/lib/libsndfile.so.1.0.37"];

    public IWaveProvider GetStream(Stream stream) => new CSCoreReader(new SndFileWaveStream(stream).ToWaveSource());

}
#endif
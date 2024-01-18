#if CSCore
using System.Composition;
using CSCore;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.NAudio;
using SilverCraft.OpenMPT;
using SilverCraft.SndFile;
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
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = new List<MimeType> { KnownMimes.WAVMime, KnownMimes.FLACMime, KnownMimes.AiffMime, KnownMimes.OGGMime, KnownMimes.MP3Mime };

    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(x));
    static readonly string[] DllLoc = ["libsndfile.so","/usr/lib/x86_64-linux-gnu/libsndfile.so",
    "libsndfile.dll","/usr/lib/x86_64-linux-gnu/libsndfile.so.1","/usr/lib/x86_64-linux-gnu/libsndfile.so.1.0.37",
    "/usr/lib/i386-linux-gnu/libsndfile.so.1","/usr/lib/i386-linux-gnu/libsndfile.so.1.0.37",
    "/usr/lib/aarch64-linux-gnu/libsndfile.so.1","/usr/lib/aarch64-linux-gnu/libsndfile.so.1.0.28", "/usr/lib/libsndfile.so", "/usr/lib/libsndfile.so.1","/usr/lib/libsndfile.so.1.0.37"];

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new SndFileWaveStream(stream).ToWaveSource());

}
//https://wiki.openmpt.org/Manual:_Module_formats
[Export(typeof(INaudioWaveStreamWrapper))]
public class LibMPT : INaudioWaveStreamWrapper
{
    public LibMPT()
    {
        if (!SupportedOnPlatform)
        {
            Log.Error("LibMPT not found, not supporting LibMPT playback");
        }
    }
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = new List<MimeType> { KnownMimes.ExtendedTrackerMime, KnownMimes.ModTrackerMime, KnownMimes.S3MTrackerMime, KnownMimes.ImpulseTrackerMime, KnownMimes.Mod3TrackerMime };
    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(x));
    static readonly string[] DllLoc = ["libopenmpt.so.0","/usr/lib/x86_64-linux-gnu/libopenmpt.so.0",
    "libopenmpt.dll","/usr/lib/x86_64-linux-gnu/libopenmpt.so.0.4.4","/usr/lib/i386-linux-gnu/libopenmpt.so.0",
    "/usr/lib/i386-linux-gnu/libopenmpt.so.0.4.4", "/usr/lib/aarch64-linux-gnu/libopenmpt.so.0.4.4",
    "/usr/lib/aarch64-linux-gnu/libopenmpt.so.0",
   ];

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new OpenMptWaveStream(stream).ToWaveSource());

}
#endif
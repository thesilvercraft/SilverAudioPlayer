using System.Composition;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.NAudio;
using SilverCraft.CSCore;
using SilverCraft.CSCore.OpenMPT;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

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
    //https://wiki.openmpt.org/Manual:_Module_formats
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.ExtendedTrackerMime, KnownMimes.ModTrackerMime, KnownMimes.S3MTrackerMime, KnownMimes.ImpulseTrackerMime, KnownMimes.Mod3TrackerMime];
    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(x));
    static readonly string[] DllLoc = ["libopenmpt.so.0","/usr/lib/x86_64-linux-gnu/libopenmpt.so.0",
        $"{AppContext.BaseDirectory}libopenmpt.dll","/usr/lib/x86_64-linux-gnu/libopenmpt.so.0.4.4","/usr/lib/i386-linux-gnu/libopenmpt.so.0",
        "/usr/lib/i386-linux-gnu/libopenmpt.so.0.4.4", "/usr/lib/aarch64-linux-gnu/libopenmpt.so.0.4.4",
        "/usr/lib/aarch64-linux-gnu/libopenmpt.so.0","/usr/lib/libopenmpt.so","/usr/lib/libopenmpt.so.0","usr/lib/libopenmpt.so.0.4.4",
    ];

    public IWaveProvider GetStream(Stream stream) => new CSCoreReader(new OpenMptWaveStream(stream).ToWaveSource());

}

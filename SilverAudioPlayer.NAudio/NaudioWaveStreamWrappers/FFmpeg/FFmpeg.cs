#if CSCore
#if FFmpeg
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.NAudio;
using SilverMagicBytes;
using DynamicallyLoadedBindings = FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class FFmpeg : INaudioWaveStreamWrapper
{
    public FFmpeg()
    {
        if (!SupportedOnPlatform)
        {
            Log.Error("ffmpeg not found, not supporting ffmpeg playback");
        }
        else
        {
            unsafe
            {
                Console.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
                
                if (OperatingSystem.IsLinux())
                {
                    DynamicallyLoadedBindings.LibrariesPath = "/usr/lib/";
                    ffmpeg.RootPath = "/usr/lib/";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var current = AppContext.BaseDirectory;
                    string[] placesToCheck = [Path.Combine(current, "FFmpeg", "bin", Environment.Is64BitProcess ? "x64" : "x86"),
                        current
                    ];
                    foreach (var binaries in placesToCheck)
                    {
                        if (!Directory.Exists(binaries)) continue;
                        ffmpeg.RootPath = binaries;
                        return;
                    }
                }
                DynamicallyLoadedBindings.Initialize();
                Log.Information($"FFmpeg version info: {ffmpeg.av_version_info()}");
                ffmpeg.av_log_set_level(Debugger.IsAttached ? ffmpeg.AV_LOG_DEBUG : ffmpeg.AV_LOG_INFO);

                av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
                {
                    if (level > ffmpeg.av_log_get_level()) return;

                    var lineSize = 1024;
                    var lineBuffer = stackalloc byte[lineSize];
                    var printPrefix = 1;
                    ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                    var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
                    Debug.WriteLine(line);
                    switch (level)
                    {
                        case ffmpeg.AV_LOG_DEBUG:
                            Log.Debug(line);
                            break;
                        case ffmpeg.AV_LOG_ERROR:
                            Log.Error(line);
                            break;
                       
                        case ffmpeg.AV_LOG_PANIC:
                        case ffmpeg.AV_LOG_FATAL:
                            Log.Fatal(line);
                            break;
                        case ffmpeg.AV_LOG_VERBOSE:
                        case ffmpeg.AV_LOG_TRACE:
                            Log.Verbose(line);
                            break;
                        case ffmpeg.AV_LOG_WARNING:
                            Log.Warning(line);
                            break;
                        case ffmpeg.AV_LOG_INFO:
                        default:
                            Log.Information(line);
                            break;
                            
                    }
                };
                ffmpeg.av_log_set_callback(logCallback);
            }
        }
    }
    public IReadOnlyList<MimeType> SupportedMimeTypes { get; } = [KnownMimes.MKVMime];
    public bool SupportedOnPlatform => DllLoc.Any(x => Path.Exists(Path.GetFullPath(x,AppContext.BaseDirectory)));
    static readonly string[] DllLoc = ["/usr/lib/libavcodec.so","libavcodec-58.dll","avcodec-59.dll"];

    public WaveStream GetStream(Stream stream) => new CSCoreReader(new FFMpegWaveStream(stream));

}
#endif
#endif

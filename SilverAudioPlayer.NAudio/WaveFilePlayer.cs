using System.Composition;
using System.Diagnostics;
using Audioz_playerZ;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.Shared;
#if CSCore
using SilverCraft.PortAudio;
using CSCore.SoundOut;
#endif
using SilverMagicBytes;

using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;
namespace SilverAudioPlayer.NAudio;

[Export(typeof(IPlay))]
public class WaveFilePlayer : IDisposable, IPlayStreams, IPlayWithSelector, IPlayerLoop
{
    public WaveStream? audioFile;
    private IWavePlayer? outputDevice;
    ILogger? logger;
    public WaveFilePlayer()
    {
        Debug.WriteLine("NewInstance of WaveFilePlayer");
        logger = Logger.GetLogger(typeof(WaveFilePlayer));
    }

    static WaveFilePlayer()
    {
        CSCore.LogLocation.GetFunc = Logger.GetLogger;
    }
    public static bool IsBackendAvaliable()
    {
#if CSCore
        return OperatingSystem.IsWindows() || PortAudioSoundOut.IsSupported() || ALSoundOut.IsSupported;
#else
        return OperatingSystem.IsWindows();
#endif
    }

    public void InitBackEnd(byte volume)
    {

#if CSCore
        if (!OperatingSystem.IsWindows())
        {
            if (PortAudioSoundOut.IsSupported())
            {
                logger?.Debug("initing backend CSCorePlayer PortAudioSoundOut");
                outputDevice = new Any.PlayProvider.NAudio.CSCorePlayer(new PortAudioSoundOut(50));
            }
            if (ALSoundOut.IsSupported)
            {
                logger?.Debug("initing backend CSCorePlayer ALSoundOut");
                outputDevice = new Any.PlayProvider.NAudio.CSCorePlayer(new ALSoundOut(50));
            }
        }
        else
#endif
        {
            logger?.Debug("initing backend naudio WaveOutEvent");

            outputDevice = new WaveOutEvent()
            {
                Volume = volume / 100f
            };
        }

    }

    public byte Volume { get; set; } = 70;
    public string? Decoder { get; private set; }

    public void Dispose()
    {
        Debug.WriteLine("Disposing WaveFilePlayer");
        GC.SuppressFinalize(this);
        foreach (var file in FilesToCleanUp.ToArray())
        {
            File.Delete(file);
            FilesToCleanUp.Remove(file);
        }
        if (outputDevice != null)
        {
            outputDevice.Dispose();
            outputDevice = null;
        }
        audioFile?.Dispose();
        logger = null;
        if (LoadedStream is { CanRead: true })
        {
            LoadedStream.Dispose();
        }
    }

    public void Play()
    {
        outputDevice?.Play();
    }

    public void Stop()
    {
        outputDevice?.Stop();
    }

    public void Pause()
    {
        outputDevice?.Pause();
        TrackPause?.Invoke(null, audioFile);
    }

    public void Resume()
    {
        outputDevice?.Play();
    }

    public void SetVolume(byte volume)
    {
        Volume = volume;
        if (outputDevice != null) outputDevice.Volume = volume / 100f;
    }

    public TimeSpan GetPosition()
    {
        if (outputDevice != null && audioFile is { } ws) return ws.CurrentTime;
        //this should not happen
        logger?.Error("PlayProvider.NAudio THISSHOULDNOTHAPPEN GETPOS :(");
        return TimeSpan.Zero;
    }

    public void SetPosition(TimeSpan position)
    {
        if (outputDevice != null && audioFile is WaveStream ws) ws.SetPosition(position);
    }

    public PlaybackState? GetPlaybackState()
    {
        return outputDevice?.PlaybackState switch
        {
            global::NAudio.Wave.PlaybackState.Stopped => PlaybackState.Stopped,
            global::NAudio.Wave.PlaybackState.Playing => PlaybackState.Playing,
            global::NAudio.Wave.PlaybackState.Paused => PlaybackState.Paused,
            _ => null
        };
    }

    public TimeSpan? Length()
    {
        return audioFile?.TotalTime;
    }

    public uint? ChannelCount()
    {
        if (audioFile != null) return (uint?)audioFile.WaveFormat.Channels;

        return null;
    }

    public long? GetSampleRate()
    {
        return audioFile?.WaveFormat.SampleRate;
    }

    public uint? GetBitsPerSample()
    {
        if (audioFile != null) return (uint?)audioFile.WaveFormat.BitsPerSample;

        return null;
    }

    public event EventHandler<object> TrackEnd;

    public event EventHandler<object> TrackPause;
    public WrappedStream? LoadedWrappedStream { get; set; }
    public Stream? LoadedStream { get; set; }
    readonly List<string> FilesToCleanUp = [];
    public void LoadStream(WrappedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.MimeType == KnownMimes.MKVMime && File.Exists("/usr/bin/mkvextract"))
        {
            logger?.Debug("Doing silly stuff for mkv");

            var a = Path.GetTempPath() + "Sap" + Random.Shared.Next() + ".webm";
            var b = Path.GetTempPath() + "Sap" + Random.Shared.Next() + ".opus";
            using var fileWrite = File.OpenWrite(a);
            stream.Use(x => x.CopyTo(fileWrite));
            fileWrite.Close();
            FilesToCleanUp.Add(a);
            FilesToCleanUp.Add(b);
            var c = Process.Start("/usr/bin/mkvextract", $"{a} tracks 0:{b}");
            c.WaitForExit();
            File.Delete(a);
            FilesToCleanUp.Remove(a);
            if (c.ExitCode == 0)
            {
                stream = new WrappedFileStream(b);
            }
            else
            {
                //nevermind
            }
        }
        var wrapper = NaudioWaveStreamWrapperTypeHolder.Get().GetWrapper(stream);
        if (wrapper != null)
        {
            Decoder = wrapper.GetType().FullName + " (" + stream.MimeType + ")";
            LoadedStream = stream.GetStream();
            audioFile = wrapper.GetStream(LoadedStream);
        }
        else if (stream is WrappedFileStream wf)
        {
            Decoder = "NAudio.Wave.AudioFileReader";
            audioFile = new AudioFileReader(wf.URL);
        }
        logger?.Information("Using {Decoder}", Decoder);

        if (outputDevice == null)
        {
            InitBackEnd(Volume);
            outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
        }
        else
            outputDevice.Stop();

        outputDevice?.Init(audioFile);
        LoadedWrappedStream = stream;
    }

    public IReadOnlyList<MimeType>? SupportedMimes => NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers()
        .SelectMany(x => x.SupportedMimeTypes).ToList();

    public int CurrentPattern => GetCurrentPattern();

    public int NumberOfPatterns => GetNumPattern();

    public bool CanLoop => GetCanLoop();

    private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        TrackEnd?.Invoke(sender, e);
    }
    bool GetCanLoop()
    {
        if (audioFile is ILoop mselector)
        {
            return mselector.CanLoop;
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ILoop cselector)
            {
                return cselector.CanLoop;
            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ILoop selector)
            {
                return selector.CanLoop;
            }
        }
        return false;
    }
    int GetCurrentPattern()
    {
        if (audioFile is ISelector mselector)
        {
            return mselector.CurrentPattern;
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ISelector cselector)
            {
                return cselector.CurrentPattern;
            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ISelector selector)
            {
                return selector.CurrentPattern;
            }
        }
        return 0;
    }
    int GetNumPattern()
    {
        if (audioFile is ISelector mselector)
        {
            return mselector.NumberOfPatterns;
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ISelector cselector)
            {
                return cselector.NumberOfPatterns;
            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ISelector selector)
            {
                return selector.NumberOfPatterns;
            }
        }
        return 0;
    }
    public void SetPattern(int pattern)
    {
        if (audioFile is ISelector mselector)
        {
            mselector.SetPattern(pattern);
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ISelector cselector)
            {
                cselector.SetPattern(pattern);
            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ISelector selector)
            {
                selector.SetPattern(pattern);
            }
        }
    }

    public void EnableLoop()
    {
        if (audioFile is ILoop mselector)
        {
            mselector.EnableLoop();
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ILoop cselector)
            {
                cselector.EnableLoop();

            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ILoop selector)
            {
                selector.EnableLoop();

            }
        }
    }

    public void DisableLoop()
    {
        if (audioFile is ILoop mselector)
        {
            mselector.DisableLoop();
        }
        else if (audioFile is Any.PlayProvider.NAudio.NaudioWaveStreamWrappers.CSCoreReader cSCoreReader)
        {
            if (cSCoreReader._reader is ILoop cselector)
            {
                cselector.DisableLoop();

            }
            else if (cSCoreReader._reader is CSCore.Streams.SampleConverter.SampleToWaveBase sampleToIeeeFloat32 && sampleToIeeeFloat32.Source is ILoop selector)
            {
                selector.DisableLoop();

            }
        }
    }
}
using System.Composition;
using System.Diagnostics;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.Any.PlayProvider.NAudio;
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
    public bool IsTrackLoaded => audioFile!=null;
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

    public void InitBackEnd()
    {

#if CSCore
        if (!OperatingSystem.IsWindows())
        {
            if (PortAudioSoundOut.IsSupported())
            {
                logger?.Information("initing backend CSCorePlayer PortAudioSoundOut");
                outputDevice = new CSCorePlayer(new PortAudioSoundOut());
            }
            else if (ALSoundOut.IsSupported)
            {
                logger?.Information("initing backend CSCorePlayer ALSoundOut");
                outputDevice = new CSCorePlayer(new ALSoundOut(50));
            }
        }
        else
#endif
        {
            logger?.Information("initing backend naudio WaveOutEvent");
            outputDevice = new WaveOutEvent();
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
        audioFile?.Dispose();
        audioFile=null;
        if (LoadedStream is { CanRead: true })
        {
            LoadedStream.Dispose();
        }
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
    public IPlayerEnvironment? playerEnvironment;
    public WaveConfig Config;
    float ReplayGainMultiplier = 1;
    bool SearchForReplayGain = true;
    float dBToRatio(float dB)
    {
        return MathF.Pow(10, dB / 20);
    }
    public void SetVolume(byte volume)
    {
        if (!SearchForReplayGain)
        {
            Volume = volume;
            if (outputDevice != null) outputDevice.Volume = volume * ReplayGainMultiplier / 100f;
        }
        else
        {
            bool rgSuccess = false;
            bool Album = false;
            SearchForReplayGain = false;
            if (playerEnvironment == null) return;
            var e = playerEnvironment.GetEntriesForCurrentTrack();
            float? TryGetReplayGainPeak(bool album)
            {
                if (e!=null && e.TryGetValue($"REPLAYGAIN_{(album ? "ALBUM" : "TRACK")}_PEAK", out string al) && float.TryParse(al, out var r))
                {
                    return r;
                }
                return null;
            }
            if (e != null)
            {
                
            bool TryGetReplayGain(bool album)
            {
                if (!e.TryGetValue($"REPLAYGAIN_{(album ? "ALBUM" : "TRACK")}_GAIN", out string al) ||
                    !float.TryParse(al.Split(' ')[0], out var r)) return false;
                ReplayGainMultiplier = dBToRatio(r);
                return true;
            }
         
        
            if (Config.PreferAlbumReplayGain)
            {
                rgSuccess = TryGetReplayGain(true);
                Album = true;
                if (!rgSuccess)
                {
                    rgSuccess = TryGetReplayGain(false);
                    Album = false;
                }
            }
            else
            {
                rgSuccess = TryGetReplayGain(false);
                Album = false;
                if (!rgSuccess)
                {
                    rgSuccess = TryGetReplayGain(true);
                    Album = true;
                }
            }
            }

            if (rgSuccess)
            {
                if (Config.TrySafeReplayGain && TryGetReplayGainPeak(Album) is { } val)
                {
                    if (val != 1)
                    {
                        ReplayGainMultiplier *= dBToRatio( Config.PreampReplayGain);
                    }

                    if (!(val * ReplayGainMultiplier > 1)) return;
                    Debug.WriteLine($"RGM {ReplayGainMultiplier} PEAKS");
                    ReplayGainMultiplier = 1f / val;
                    Debug.WriteLine($"RGM went to max safe {ReplayGainMultiplier}");
                }
                else
                {
                    ReplayGainMultiplier *= dBToRatio(Config.PreampReplayGain);
                }
            }
            else
            {
                ReplayGainMultiplier = dBToRatio(Config.PreampNoReplayGain);
            }
        }
    }

    public TimeSpan GetPosition()
    {
        if (outputDevice != null && audioFile is { } ws) return ws.CurrentTime;
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
            logger?.Warning("Doing silly stuff for mkv");
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
        var wrappers = NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers(stream);

        var naudioWaveStreamWrappers = wrappers.ToList();
        if (naudioWaveStreamWrappers.Count != 0)
        {
            foreach (var wrapper in naudioWaveStreamWrappers)
            {
                try
                {
                    Decoder = wrapper.GetType().FullName + " (" + stream.MimeType + ")";
                    LoadedStream = stream.GetStream();
                    audioFile = wrapper.GetStream(LoadedStream);
                    break;
                }
                catch (Exception e)
                {
                    logger?.Error(e,"Failed to use {WrapperType}", wrapper.GetType());
                }
            }
        }
        else if (stream is WrappedFileStream wf)
        {
            Decoder = "NAudio.Wave.AudioFileReader";
            audioFile = new AudioFileReader(wf.URL);
        }
        logger?.Information("Using {Decoder}", Decoder);

        if (outputDevice == null)
        {
            InitBackEnd();
            outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
        }
        else
            outputDevice.Stop();
        ReplayGainMultiplier = 1;
        SearchForReplayGain = Config?.EnableReplayGain ?? false;
        SetVolume(Volume);
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
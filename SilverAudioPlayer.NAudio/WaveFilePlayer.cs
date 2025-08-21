#if CSCore
using SilverCraft.CSCore.PortAudio;
using SilverCraft.CSCore.SoundOut;
#endif
using System.Composition;
using System.Diagnostics;
using System.Globalization;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.Any.PlayProvider.NAudio;
using SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;
using SilverAudioPlayer.Shared;
using SilverCraft.CSCore;
using SilverCraft.CSCore.Streams.SampleConverter;
using SilverMagicBytes;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;
using StoppedEventArgs = NAudio.Wave.StoppedEventArgs;

namespace SilverAudioPlayer.NAudio;

[Export(typeof(IPlay))]
public class WaveFilePlayer : IDisposable, IPlayStreams, IPlayWithSelector, IPlayerLoop
{
    private readonly List<string> FilesToCleanUp = [];
    public IWaveProvider? audioFile;
    public WaveConfig Config;
    private ILogger? logger;
    public IWavePlayer? outputDevice;
    public IPlayerEnvironment? playerEnvironment;
    private float ReplayGainMultiplier = 1;
    private bool SearchForReplayGain = true;

    static WaveFilePlayer()
    {
        LogLocation.GetFunc = Logger.GetLogger;
    }

    public WaveFilePlayer()
    {
        Debug.WriteLine("NewInstance of WaveFilePlayer");
        logger = Logger.GetLogger(typeof(WaveFilePlayer));
    }

    public byte Volume { get; set; } = 70;
    public string? Decoder { get; private set; }
    public WrappedStream? LoadedWrappedStream { get; set; }
    public Stream? LoadedStream { get; set; }

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

        if (audioFile is IDisposable disposableaudioFile)
        {
            disposableaudioFile.Dispose();
        }
        logger = null;
        if (LoadedStream is { CanRead: true }) LoadedStream.Dispose();
    }

    public bool CanLoop => GetCanLoop();

    public void EnableLoop()
    {
        if (audioFile is ILoop mselector)
        {
            mselector.EnableLoop();
        }
        else if (audioFile is CSCoreReader cSCoreReader)
        {
            switch (cSCoreReader._reader)
            {
                case ILoop cselector:
                    cselector.EnableLoop();
                    break;
                case SampleToWaveBase { Source: ILoop selector }:
                    selector.EnableLoop();
                    break;
            }
        }
    }

    public void DisableLoop()
    {
        if (audioFile is ILoop mselector)
        {
            mselector.DisableLoop();
        }
        else if (audioFile is CSCoreReader cSCoreReader)
        {
            switch (cSCoreReader._reader)
            {
                case ILoop cselector:
                    cselector.DisableLoop();
                    break;
                case SampleToWaveBase { Source: ILoop selector }:
                    selector.DisableLoop();
                    break;
            }
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;
    public bool IsTrackLoaded => audioFile != null;

    public void Play()
    {
        outputDevice?.Play();
    }

    public void Stop()
    {
        outputDevice?.Stop();
        
        if (audioFile is IDisposable disposableaudioFile)
        {
            disposableaudioFile.Dispose();
        }
        audioFile = null;
        if (LoadedStream is { CanRead: true }) LoadedStream.Dispose();
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
        if (!SearchForReplayGain)
        {
            Volume = volume;
            if (outputDevice != null) outputDevice.Volume = Math.Min(ReplayGainMultiplier*(volume / 100f), 1);
        }
        else
        {
            logger?.Information("searching for replaygain");
            var rgSuccess = false;
            var album = false;
            SearchForReplayGain = false;
            if (playerEnvironment == null) return;
            var e = playerEnvironment.GetEntriesForCurrentTrack();
            if(e==null) return;
            float? TryGetReplayGainPeak(bool album)
            {
                if (e != null && e.TryGetValue($"REPLAYGAIN_{(album ? "ALBUM" : "TRACK")}_PEAK", out var al) &&
                    float.TryParse(al, CultureInfo.InvariantCulture, out var r)) return r;
                return null;
            }
                bool TryGetReplayGain(bool album)
                {
                    if (!e.TryGetValue($"REPLAYGAIN_{(album ? "ALBUM" : "TRACK")}_GAIN", out var al) ||
                        !float.TryParse(al.Split(' ')[0], CultureInfo.InvariantCulture, out var r)) return false;
                    ReplayGainMultiplier = dBToRatio(r);
                    return true;
                }


                if (Config.PreferAlbumReplayGain)
                {
                    rgSuccess = TryGetReplayGain(true);
                    album = true;
                    if (!rgSuccess)
                    {
                        rgSuccess = TryGetReplayGain(false);
                        album = false;
                    }
                }
                else
                {
                    rgSuccess = TryGetReplayGain(false);
                    album = false;
                    if (!rgSuccess)
                    {
                        rgSuccess = TryGetReplayGain(true);
                        album = true;
                    }
                }

            if (rgSuccess)
            {
                if (Config.TrySafeReplayGain && TryGetReplayGainPeak(album) is { } val)
                {
                    if (val != 1) ReplayGainMultiplier *= dBToRatio(Config.PreampReplayGain);
                    if (!(val * ReplayGainMultiplier > 1)) return;
                    logger?.Information("ReplayGainMultiplier peaks, setting to 1/Peak");
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
        if (outputDevice != null)
        {
            return audioFile switch
            {
                WaveStream ws => ws.CurrentTime,
                IWaveStream iws => iws.CurrentTime,
                _ => TimeSpan.Zero
            };
        }
        return TimeSpan.Zero;
    }

    public void SetPosition(TimeSpan position)
    {
        if (outputDevice == null) return;
        switch (audioFile)
        {
            case WaveStream ws:
                ws.SetPosition(position);
                break;
            case IWaveStream iws:
                iws.SetPosition(position);
                break;
            default:
                return;
        }
        PositionChanged?.Invoke(this, position);
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
        if (audioFile != null)
        {
            return audioFile switch
            {
                WaveStream ws => ws.TotalTime,
                IWaveStream iws => iws.TotalTime,
                _ => TimeSpan.Zero
            };
        }
        return TimeSpan.Zero;
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

    public void LoadStream(WrappedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var wrappers = NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers(stream);
        if (audioFile is IDisposable disposable)
        {
            outputDevice?.Stop();
            disposable.Dispose();
        }
        var naudioWaveStreamWrappers = wrappers.ToList();
        if (naudioWaveStreamWrappers.Count != 0)
        {
            foreach (var wrapper in naudioWaveStreamWrappers)
                try
                {
                    Decoder = wrapper.GetType().FullName + " (" + stream.MimeType + ")";
                    LoadedStream = stream.GetStream();
                    audioFile = wrapper.GetStream(LoadedStream);
                    break;
                }
                catch (Exception e)
                {
                    logger?.Error(e, "Failed to use {WrapperType}", wrapper.GetType());
                }
        }
        else if (stream is WrappedFileStream wf)
        {
            Decoder = "NAudio.Wave.AudioFileReader";
            audioFile = new AudioFileReader(wf.URL);
        }

        if (audioFile == null)
        {
            throw new NotSupportedException();
        }
        logger?.Information("Using {Decoder}", Decoder);

        if (outputDevice == null)
        {
            InitBackEnd();
            if(outputDevice!=null)
            {
                outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
            }
        }
        else
        {
            outputDevice.Stop();
        }

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

    public void SetPattern(int pattern)
    {
        switch (audioFile)
        {
            case ISelector mselector:
                mselector.SetPattern(pattern);
                break;
            case CSCoreReader cSCoreReader:
            {
                switch (cSCoreReader._reader)
                {
                    case ISelector cselector:
                        cselector.SetPattern(pattern);
                        break;
                    case SampleToWaveBase { Source: ISelector selector }:
                        selector.SetPattern(pattern);
                        break;
                }
                break;
            }
        }
    }

    public static bool IsBackendAvaliable()
    {
#if CSCore
        return OperatingSystem.IsWindows() || PortAudioSoundOut.IsSupported() || ALSoundOut.IsSupported;
#else
        return OperatingSystem.IsWindows();
#endif
    }

    bool? BlacklistPortAudio =null;
    bool? CheckForBlocklist()
    {
        if(BlacklistPortAudio!= null)
        {
            return BlacklistPortAudio;
        } 
        if(Environment.GetEnvironmentVariable("SAPBLOCKPORTAUDIO")!=null)
        {
             return BlacklistPortAudio=true;
        }
        return BlacklistPortAudio=false;
    }
    public void InitBackEnd()
    {
#if CSCore
        if (!OperatingSystem.IsWindows())
        {
            if (PortAudioSoundOut.IsSupported() && CheckForBlocklist()!=true)
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
            outputDevice = new WavePlayerCompatibilityLayer(new WaveOutEvent());
        }
    }

    private float dBToRatio(float dB)
    {
        return MathF.Pow(10, dB / 20);
    }

    private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        TrackEnd?.Invoke(sender, e);
    }

    private bool GetCanLoop()
    {
        return audioFile switch
        {
            ILoop mselector => mselector.CanLoop,
            CSCoreReader { _reader: ILoop cselector } => cselector.CanLoop,
            CSCoreReader { _reader: SampleToWaveBase { Source: ILoop selector } } => selector.CanLoop,
            _ => false
        };
    }

    private int GetCurrentPattern()
    {
        return audioFile switch
        {
            ISelector mselector => mselector.CurrentPattern,
            CSCoreReader { _reader: ISelector cselector } => cselector.CurrentPattern,
            CSCoreReader { _reader: SampleToWaveBase { Source: ISelector selector } } => selector.CurrentPattern,
            _ => 0
        };
    }

    private int GetNumPattern()
    {
        return audioFile switch
        {
            ISelector mselector => mselector.NumberOfPatterns,
            CSCoreReader { _reader: ISelector cselector } => cselector.NumberOfPatterns,
            CSCoreReader { _reader: SampleToWaveBase { Source: ISelector selector } } =>
                selector.NumberOfPatterns,
            _ => 0
        };
    }
}
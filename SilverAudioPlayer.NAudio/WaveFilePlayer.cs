using SilverCraft.CSCore.PortAudio;
using SilverCraft.CSCore.SoundOut;
using System.Composition;
using System.Diagnostics;
using NAudio.Wave;
using Serilog;
using SilverAudioPlayer.Any.PlayProvider.NAudio;
using SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;
using SilverCraft.CSCore.Streams.SampleConverter;
using SilverMagicBytes;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;
using StoppedEventArgs = NAudio.Wave.StoppedEventArgs;

namespace SilverAudioPlayer.NAudio;

[Export(typeof(IPlay))]
public class WaveFilePlayer : IDisposable, IPlayStreams, IPlayWithSelector, IPlayerLoop, IPlayLancer
{
    public IWaveProvider? audioFile;
    public WaveConfig Config;
    public IWavePlayer? outputDevice;
    public IPlayerEnvironment? playerEnvironment;
    private float ReplayGainMultiplier = 1;
    private bool SearchForReplayGain = true;
    private IMediator _mediator;

    private GaplessWaveProvider? _gaplessWaveProvider;
    private IWaveProvider? _currentWaveProvider;
    private WrappedStream? _currentWrappedStream;


    public WaveFilePlayer(IMediator m)
    {
        Debug.WriteLine("NewInstance of WaveFilePlayer");
        _mediator = m;
    }

    public byte Volume { get; set; } = 70;
    public string? Decoder { get; private set; }
    public WrappedStream? LoadedWrappedStream { get; set; }
    public Stream? LoadedStream { get; set; }

    public void Dispose()
    {
        Debug.WriteLine("Disposing WaveFilePlayer");
        GC.SuppressFinalize(this);

        _gaplessWaveProvider?.Dispose();
        _gaplessWaveProvider = null;
        _currentWrappedStream?.Dispose();
        _currentWrappedStream = null;

        if (outputDevice != null)
        {
            outputDevice.Dispose();
            outputDevice = null;
        }

        if (audioFile is IDisposable disposableaudioFile)
        {
            disposableaudioFile.Dispose();
        }
        
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
    public event EventHandler? NextStreamStarted;
    public bool IsTrackLoaded => audioFile != null;

    public void Play()
    {
        outputDevice?.Play();
    }

    public void Stop()
    {
        outputDevice?.Stop();

        if (_gaplessWaveProvider != null)
        {
            _gaplessWaveProvider.CurrentProviderChanged -= GaplessWaveProvider_CurrentProviderChanged;
            _gaplessWaveProvider.Dispose();
            _gaplessWaveProvider = null;
        }

        _currentWrappedStream?.Dispose();
        _currentWrappedStream = null;

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
        Volume = volume;

        if (SearchForReplayGain)
        {
            Log.Information("Searching for ReplayGain...");
            SearchForReplayGain = false;

            var result = _mediator.RequestQuery(RequestReplayGainInfo.Instance);

            if (result is { Success: true, Gain: { } gainValue })
            {
                float targetGainMultiplier = dBToRatio(gainValue);
                float preampMultiplier = dBToRatio(Config.PreampReplayGain);

                float calculatedMultiplier = targetGainMultiplier * preampMultiplier;

                if (Config.TrySafeReplayGain && result.Peak is { } peakVal && peakVal > 0)
                {
                    if (peakVal * calculatedMultiplier > 1.0f)
                    {
                        Log.Information("ReplayGainMultiplier peaks, setting to 1/Peak");
                        calculatedMultiplier = 1.0f / peakVal;
                        Debug.WriteLine($"RGM went to max safe limit: {calculatedMultiplier}");
                    }
                }

                ReplayGainMultiplier = calculatedMultiplier;
            }
            else
            {
                ReplayGainMultiplier = dBToRatio(Config.PreampNoReplayGain);
            }
        }

        if (outputDevice != null)
        {
            outputDevice.Volume = Math.Min(ReplayGainMultiplier * (Volume / 100f), 1.0f);
        }
    }

    public TimeSpan GetPosition()
    {
        if (outputDevice != null)
        {
            var currentProvider = _gaplessWaveProvider?.CurrentProvider ?? audioFile;
            return currentProvider switch
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
        var currentProvider = _gaplessWaveProvider?.CurrentProvider ?? audioFile;
        switch (currentProvider)
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
        var currentProvider = _gaplessWaveProvider?.CurrentProvider ?? audioFile;
        if (currentProvider != null)
        {
            return currentProvider switch
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
                    Log.Error(e, "Failed to use {WrapperType}", wrapper.GetType());
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
        Log.Information("Using {Decoder}", Decoder);
        var entries = playerEnvironment?.GetEntriesForCurrentTrack();
        if (entries != null)
        {
            if (audioFile is not ILoop and not CSCoreReader { _reader: SampleToWaveBase { Source: ILoop  } } and not CSCoreReader { _reader: ILoop}
                && entries.TryGetValue("LOOPSTART", out var loopStart))
            {
                long loopStartl;
                if (long.TryParse(loopStart, out loopStartl))
                {
                    long loopEndl = 0;
                    var success = false;
                    if (entries.TryGetValue("LOOPEND", out var loopEnd))
                    {
                        success=long.TryParse(loopEnd, out loopEndl);
                    }
                    else if(entries.TryGetValue("LOOPLENGTH", out var loopLength))
                    {
                        success=  long.TryParse(loopLength, out loopEndl);
                        loopEndl += loopStartl;
                    }

                    if (success)
                    {
                        audioFile = audioFile switch
                        {
                            WaveStream ws => new LoopableAudioSource(ws, loopStartl, loopEndl),
                            IWaveStream iws => new LoopableAudioSource(iws, loopStartl, loopEndl),
                            _ => audioFile
                        };
                    }
                }
            }
        }

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
            outputDevice.PlaybackStopped -= OutputDeviceOnPlaybackStopped;
            outputDevice.Stop();
            outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
        }

        _currentWaveProvider = audioFile;
        _currentWrappedStream = stream;
        if (_gaplessWaveProvider != null)
        {
            _gaplessWaveProvider.CurrentProviderChanged -= GaplessWaveProvider_CurrentProviderChanged;
            _gaplessWaveProvider.Dispose();
        }
        _gaplessWaveProvider = new GaplessWaveProvider(_currentWaveProvider);
        _gaplessWaveProvider.CurrentProviderChanged += GaplessWaveProvider_CurrentProviderChanged;

        ReplayGainMultiplier = 1;
        SearchForReplayGain = Config?.EnableReplayGain ?? false;
        SetVolume(Volume);
        outputDevice?.Init(_gaplessWaveProvider);
        LoadedWrappedStream = stream;
    }

    public bool EnqueueNextStream(WrappedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_gaplessWaveProvider == null || _currentWaveProvider == null) return false;

        var wrappers = NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers(stream).ToList();
        if (wrappers.Count != 0)
        {
            foreach (var wrapper in wrappers)
            {
                try
                {
                    var nextLoadedStream = stream.GetStream();
                    var nextWaveProvider = wrapper.GetStream(nextLoadedStream);
                    return _gaplessWaveProvider.Enqueue(nextWaveProvider);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to enqueue next stream using {WrapperType}", wrapper.GetType());
                }
            }
        }
        else if (stream is WrappedFileStream wf)
        {
            try
            {
                var nextWaveProvider = new AudioFileReader(wf.URL);
                return _gaplessWaveProvider.Enqueue(nextWaveProvider);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to enqueue next file stream");
            }
        }

        return false;
    }

    public bool ReplaceNextStream(WrappedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_gaplessWaveProvider == null || _currentWaveProvider == null) return false;

        var wrappers = NaudioWaveStreamWrapperTypeHolder.Get().GetWrappers(stream).ToList();
        if (wrappers.Count != 0)
        {
            foreach (var wrapper in wrappers)
            {
                try
                {
                    var nextLoadedStream = stream.GetStream();
                    var nextWaveProvider = wrapper.GetStream(nextLoadedStream);
                    return _gaplessWaveProvider.ReplaceNextProvider(nextWaveProvider);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to replace next stream using {WrapperType}", wrapper.GetType());
                }
            }
        }
        else if (stream is WrappedFileStream wf)
        {
            try
            {
                var nextWaveProvider = new AudioFileReader(wf.URL);
                return _gaplessWaveProvider.ReplaceNextProvider(nextWaveProvider);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to replace next file stream");
            }
        }

        return false;
    }

    private void GaplessWaveProvider_CurrentProviderChanged(object? sender, EventArgs e)
    {
        if (_gaplessWaveProvider != null)
        {
            audioFile = _gaplessWaveProvider.CurrentProvider;
        }

        NextStreamStarted?.Invoke(this, EventArgs.Empty);
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
        return OperatingSystem.IsWindows() || PortAudioSoundOut.IsSupported() || ALSoundOut.IsSupported;
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
        if (!OperatingSystem.IsWindows())
        {
            if (PortAudioSoundOut.IsSupported() && CheckForBlocklist()!=true)
            {
                Log.Information("initing backend CSCorePlayer PortAudioSoundOut");
                outputDevice = new CSCorePlayer(new PortAudioSoundOut());
            }
            else if (ALSoundOut.IsSupported)
            {
                Log.Information("initing backend CSCorePlayer ALSoundOut");
                outputDevice = new CSCorePlayer(new ALSoundOut(50));
            }
        }
        else
        {
            Log.Information("initing backend naudio WaveOutEvent");
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
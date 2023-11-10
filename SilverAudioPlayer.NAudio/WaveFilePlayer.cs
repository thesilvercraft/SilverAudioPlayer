using System.Composition;
using System.Diagnostics;
using Audioz_playerZ;
using CSCore.SoundOut;
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;

namespace SilverAudioPlayer.NAudio;



[Export(typeof(IPlay))]
public class WaveFilePlayer : IDisposable, IPlayStreams
{
    public WaveStream? audioFile;
    private IWavePlayer? outputDevice;

    public WaveFilePlayer()
    {
        Debug.WriteLine("NewInstance of WaveFilePlayer");
    }

    static WaveFilePlayer()
    {
        CSCore.LogLocation.GetFunc = Logger.GetLogger;
    }
    public void InitBackEnd(byte volume)
    {
#if CSCore
        if (OperatingSystem.IsWindows())
        {
            outputDevice = new WaveOutEvent()
            {
                Volume = volume / 100f
            };
        }
        else
        {
            outputDevice = new SilverAudioPlayer.Any.PlayProvider.NAudio.CSCorePlayer(new ALSoundOut(50));
        }
#else
     outputDevice = new WaveOutEvent()
            {
                Volume = volume / 100f
            };

#endif
    }

    public byte Volume { get; set; } = 70;
    public string? Decoder { get; private set; }

    public void Dispose()
    {
        Debug.WriteLine("Disposing WaveFilePlayer");
        GC.SuppressFinalize(this);
        if (outputDevice != null)
        {
            outputDevice.Dispose();
            outputDevice = null;
        }
        audioFile?.Dispose();

        if (LoadedStream is {CanRead:true})
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
        Debug.WriteLine("PlayProvider.NAudio THISSHOULDNOTHAPPEN GETPOS");
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
    public void LoadStream(WrappedStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
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

        if (outputDevice == null)
        {
            Debug.WriteLine("initing backend");
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

    private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        TrackEnd?.Invoke(sender, e);
    }
}
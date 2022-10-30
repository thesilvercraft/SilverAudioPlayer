using System.Composition;
using System.Diagnostics;
using Audioz_playerZ;
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;

namespace SilverAudioPlayer.NAudio;

[Export(typeof(IPlay))]
public class WaveFilePlayer : IPlay, IDisposable, IPlayStreams
{
    public WaveStream? audioFile;
    private IWavePlayer? outputDevice;

    public WaveFilePlayer()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            outputDevice = new WaveOutEvent();
        else
            outputDevice = new AlsaOutput();
        outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
    }

    public byte Volume { get; set; } = 70;
    public string? Decoder { get; private set; }

    public void Dispose()
    {
        if (outputDevice != null)
        {
            outputDevice.Dispose();
            outputDevice = null;
        }
    }

    public void Play()
    {
        outputDevice?.Play();
    }

    public void Stop()
    {
        outputDevice.Stop();
        outputDevice?.Dispose();
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
        if (outputDevice != null && audioFile is WaveStream ws) return ws.CurrentTime;
        //this should not happen
        Debug.WriteLine("PlayProvider.NAudio THISSHOULDNOTHAPPEN");
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
            _ => throw new NotSupportedException()
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
        if (audioFile != null) return audioFile.WaveFormat.SampleRate;

        return null;
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
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        var wrapper = NaudioWaveStreamWrapperTypeHolder.Get().GetWrapper(stream);
        if (wrapper != null)
        {
            Decoder = wrapper.GetType().FullName + " (" + stream.MimeType + ")";
            audioFile = wrapper.GetStream(stream);
        }
        else if (stream is WrappedFileStream wf)
        {
            Decoder = "NAudio.Wave.AudioFileReader";
            audioFile = new AudioFileReader(wf.URL);
        }

        DoStuffAfterFile();
    }

    private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        TrackEnd?.Invoke(sender, e);
    }

    public void LoadFromProvider(WaveStream? provider)
    {
        audioFile = provider;
        Decoder = provider?.GetType()?.FullName;
        DoStuffAfterFile();
    }

    /* public virtual void LoadFile(string file)
     {
         if (string.IsNullOrWhiteSpace(file))
         {
             throw new ArgumentException("The name of the file must not be null or empty", nameof(file));
         }
         var wrapper = NaudioWaveStreamWrapperTypeHolder.Get().GetWrapper(file);
         if (wrapper != null)
         {
             Decoder = wrapper.GetType().FullName + " (" + Path.GetExtension(file)[0..].ToUpperInvariant() + ")";
             audioFile = wrapper.GetStream(file);
         }
         else
         {
             Decoder = "NAudio.Wave.AudioFileReader";
             audioFile = new AudioFileReader(file);
         }
         DoStuffAfterFile();
     }
    */

    internal void DoStuffAfterFile()
    {
        if (outputDevice == null)
            outputDevice = new WaveOutEvent
            {
                Volume = Volume / 100f
            };
        else
            outputDevice.Stop();
        outputDevice.Init(audioFile);
    }
}
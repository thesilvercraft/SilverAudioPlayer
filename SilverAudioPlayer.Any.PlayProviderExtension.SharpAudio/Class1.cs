using System.Diagnostics;
using NAudio.Wave;
using SharpAudio;
using SharpAudio.Codec;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProviderExtension.SharpAudio;

public class SharpPlayerOutput:IWavePlayer
{
    public SoundStream? gss;

    public void Play()
    {
        gss?.Play();
    }

    public void Stop()
    {
        gss?.Stop();
    }

    public void Pause()
    {
        gss.State = SoundStreamState.Paused;
    }

    public IWaveProvider original { get; set; }
    public void Init(IWaveProvider waveProvider)
    {
        original = waveProvider;
        var engine = AudioEngine.CreateDefault();
        var sink = new SoundSink(engine);
        gss = new(new FileStream("/run/media/silver/OWH/save/music/ex-lyd/chip heat/EX-LYD - City Streets.mp3",FileMode.Open), sink);
       // gss.PlaybackStopped += (x, y) => { PlaybackStopped?.Invoke(x, y); };
    }

    public float Volume { get; set; }

    public PlaybackState PlaybackState
    {
        get
        {
            return gss?.State switch
            {
                SoundStreamState.Idle => PlaybackState.Stopped,
                SoundStreamState.Paused => PlaybackState.Paused,
                SoundStreamState.Stop => PlaybackState.Stopped,
                SoundStreamState.Playing => PlaybackState.Playing,
                SoundStreamState.PreparePlay => PlaybackState.Playing,
                SoundStreamState.TrackFinished => PlaybackState.Stopped,
                _ => PlaybackState.Stopped,
            };
        }
    }

    public WaveFormat OutputWaveFormat => original.WaveFormat;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;
    public void Dispose()
    {
    }
}
using CSCore.SoundOut;
using NAudio.Wave;
using PlaybackState = NAudio.Wave.PlaybackState;
using StoppedEventArgs = NAudio.Wave.StoppedEventArgs;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class CSCorePlayer : IWavePlayer, IDisposable, IWavePosition
{
    public CSCorePlayer(ISoundOut soundOut)
    {
        _soundOut = soundOut;
        _soundOut.Stopped += (x, y) =>
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        };
    }

    private ISoundOut _soundOut;

    public void Dispose()
    {
        _soundOut.Dispose();
    }

    public void Play()
    {
        _soundOut.Play();
    }

    public void Stop()
    {
        _soundOut.Stop();
    }

    public void Pause()
    {
        _soundOut.Pause();
    }

    private WaveInputNaudioToCsCore csCoreStream;
    public void Init(IWaveProvider waveProvider)
    {
        csCoreStream = new WaveInputNaudioToCsCore(waveProvider);
        _soundOut.Initialize(new WaveInputNaudioToCsCore(waveProvider));
    }

    public float Volume
    {
        get => _soundOut.Volume;
        set => _soundOut.Volume = value;
    }

    public PlaybackState PlaybackState => GetState();

    PlaybackState GetState()
    {
        return _soundOut.PlaybackState switch
        {
            CSCore.SoundOut.PlaybackState.Stopped => PlaybackState.Stopped,
            CSCore.SoundOut.PlaybackState.Playing => PlaybackState.Playing,
            CSCore.SoundOut.PlaybackState.Paused => PlaybackState.Paused,
            _ => PlaybackState.Stopped,
        };
    }
    public long GetPosition()
    {
        return csCoreStream.Position;
    }

    WaveFormat IWavePosition.OutputWaveFormat => csCoreStream.WaveFormat;

    WaveFormat IWavePlayer.OutputWaveFormat => csCoreStream.WaveFormat;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;
}
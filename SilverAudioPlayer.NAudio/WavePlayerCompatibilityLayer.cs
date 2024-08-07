using System.Diagnostics;
using NAudio.Wave;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class WavePlayerCompatibilityLayer : IWavePlayer
{
    private readonly IWavePlayer _implementation;

    public WavePlayerCompatibilityLayer(IWavePlayer implementation)
    {
        _implementation = implementation;
        _implementation.PlaybackStopped += OnPlaybackStop;
    }

    private void OnPlaybackStop(object? sender, StoppedEventArgs e)
    {
        if (LastManualStop != null && LastManualStop + TimeSpan.FromMilliseconds(300) >= DateTime.Now)
        {
            LastManualStop = null;
            return;
        }
        PlaybackStopped?.Invoke(this,e);
    }

    private DateTime? LastManualStop = null;
    public void Dispose()
    {
        _implementation.Dispose();
    }

    public void Play()
    {
        _implementation.Play();
    }

    public void Stop()
    {
        if (PlaybackState == PlaybackState.Stopped) return;
        _implementation.Stop();
        LastManualStop = DateTime.Now;
        Task.Run(() =>
        {
            PlaybackStopped?.Invoke(this, new());
        });
    }

    public void Pause()
    {
        _implementation.Pause();
    }

    public void Init(IWaveProvider waveProvider)
    {
        _implementation.Init(waveProvider);
    }

    public float Volume
    {
        get => _implementation.Volume;
        set => _implementation.Volume = value;
    }

    public PlaybackState PlaybackState => _implementation.PlaybackState;

    public WaveFormat OutputWaveFormat => _implementation.OutputWaveFormat;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

}
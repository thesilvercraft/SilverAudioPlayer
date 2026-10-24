using NAudio.Wave;
using Serilog;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class WavePlayerCompatibilityLayer : IWavePlayer
{
    private readonly IWavePlayer _implementation;
    private int _manualStopCount;

    public WavePlayerCompatibilityLayer(IWavePlayer implementation)
    {
        _implementation = implementation;
        _implementation.PlaybackStopped += OnPlaybackStop;
    }

    private void OnPlaybackStop(object? sender, StoppedEventArgs e)
    {
        Log.Debug("WavePlayerCompatibilityLayer.OnPlaybackStop received (Exception: {Exception}, manualStopCount: {Count})", e.Exception?.Message, _manualStopCount);
        if (_manualStopCount > 0)
        {
            Interlocked.Decrement(ref _manualStopCount);
            Log.Debug("WavePlayerCompatibilityLayer.OnPlaybackStop suppressed because of manual Stop() call (remaining: {Count})", _manualStopCount);
            return;
        }

        PlaybackStopped?.Invoke(this, e);
    }

    public void Dispose()
    {
        Log.Debug("WavePlayerCompatibilityLayer.Dispose() called");
        _implementation.Dispose();
    }

    public void Play()
    {
        Log.Debug("WavePlayerCompatibilityLayer.Play() called");
        _implementation.Play();
    }

    public void Stop()
    {
        Log.Debug("WavePlayerCompatibilityLayer.Stop() called, current state: {State}", PlaybackState);
        if (PlaybackState == PlaybackState.Stopped) return;
        Interlocked.Increment(ref _manualStopCount);
        _implementation.Stop();
    }

    public void Pause()
    {
        Log.Debug("WavePlayerCompatibilityLayer.Pause() called");
        _implementation.Pause();
    }

    public void Init(IWaveProvider waveProvider)
    {
        Log.Debug("WavePlayerCompatibilityLayer.Init() called with waveProvider of type {Type}", waveProvider.GetType().FullName);
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

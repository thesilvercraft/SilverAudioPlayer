using System.Diagnostics;
using Iot.Device.Media;
using NAudio.Wave;

namespace SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound;

public class AlsaOutput : IWavePlayer, IDisposable
{
    private CancellationTokenSource pausetoken = new();

    private WaveStream provider;
    private CancellationTokenSource resumetoken = new();
    private UnixSoundDevice sounddevice;
    private IWaveProvider source;
    private CancellationTokenSource stoptoken = new();
    private MemoryStream stream;
    private WaveFileWriter? writer;

    /// <summary>
    ///     Constructs a new WaveRecorder
    /// </summary>
    /// <param name="destination">The location to write the WAV file to</param>
    /// <param name="source">The source Wave Provider</param>
    public AlsaOutput()
    {
    }

    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    public float Volume
    {
        get => sounddevice.PlaybackVolume / 65536;
        set => sounddevice.PlaybackVolume = (long)(value * 65536);
    }

    public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;

    public WaveFormat OutputWaveFormat => source.WaveFormat;

    /// <summary>
    ///     Closes the WAV file
    /// </summary>
    public void Dispose()
    {
        Stop();
        if (writer != null)
        {
            writer.Dispose();
            writer = null;
        }
    }

    public void Play()
    {
        if (PlaybackState == PlaybackState.Paused)
        {
            PlaybackState = PlaybackState.Playing;
            Interop.snd_pcm_pause(sounddevice._playbackPcm, 0);
        }
        else
        {
            Task.Run(() =>
            {
                PlaybackState = PlaybackState.Playing;
                resumetoken = new CancellationTokenSource();
                stoptoken = new CancellationTokenSource();
                pausetoken = new CancellationTokenSource();
                var t = Task.Run(() =>
                {
                    unsafe
                    {
                        var @params = new IntPtr();
                        var dir = 0;
                        var header = sounddevice.ReadWavHeader(stream);

                        try
                        {
                            sounddevice.OpenPlaybackPcm();
                            sounddevice.PcmInitialize(sounddevice._playbackPcm, header, ref @params, ref dir);
                            Debug.WriteLine("Init finished");

                            ulong frames, bufferSize;

                            var dirP = &dir;

                            sounddevice._errorNum = Interop.snd_pcm_hw_params_get_period_size(@params, &frames, dirP);
                            sounddevice.ThrowIfError("Can not get period size.");

                            bufferSize = frames * header.BlockAlign;
                            // In Interop, the frames is defined as ulong. But actually, the value of bufferSize won't be too big.
                            var readBuffer = new byte[(int)bufferSize];

                            fixed (byte* buffer = readBuffer)
                            {
                                while (stream.Read(readBuffer, 0, readBuffer.Length) != 0)
                                {
                                    sounddevice._errorNum = Interop.snd_pcm_writei(sounddevice._playbackPcm,
                                        (IntPtr)buffer, frames);
                                    sounddevice.ThrowIfError("Can not write data to the device.");
                                }
                            }

                            Debug.WriteLine("WriteStream finished");
                            sounddevice._errorNum = Interop.snd_pcm_drain(sounddevice._playbackPcm);
                            Debug.WriteLine("Drained");
                            sounddevice.ThrowIfError("Drop playback device error.");
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                        finally
                        {
                            Dispose();
                        }
                    }
                });
                t.GetAwaiter().GetResult();
                Stop();
            });
        }
    }

    public void Stop()
    {
        if (PlaybackState is not (PlaybackState.Playing or PlaybackState.Paused)) return;
        Debug.WriteLine("Stopping");
        Interop.snd_pcm_close(sounddevice._playbackPcm);
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Pause()
    {
        Debug.WriteLine("Pausing");

        PlaybackState = PlaybackState.Paused;
        Interop.snd_pcm_pause(sounddevice._playbackPcm, 1);
    }

    public void Init(IWaveProvider waveProvider)
    {
        source = waveProvider;
        var s = new SoundConnectionSettings();
        s.RecordingBitsPerSample = 8;
        sounddevice = new UnixSoundDevice(s);
        stream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(stream, waveProvider);
        stream.Position = 0;
    }
}
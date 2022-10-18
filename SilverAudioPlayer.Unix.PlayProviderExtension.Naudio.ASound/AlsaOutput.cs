using NAudio.Wave;
using Iot.Device.Media;
using System.Diagnostics;

namespace SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound
{
    public class AlsaOutput : IWavePlayer, IDisposable
    {
        private WaveFileWriter writer;
        private IWaveProvider source;
        private UnixSoundDevice sounddevice;
        private MemoryStream stream;

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        /// <summary>
        /// Constructs a new WaveRecorder
        /// </summary>
        /// <param name="destination">The location to write the WAV file to</param>
        /// <param name="source">The source Wave Provider</param>
        public AlsaOutput()
        {
        }

        public float Volume { get => sounddevice.PlaybackVolume / 65536; set => sounddevice.PlaybackVolume = (long)(value * 65536); }

        public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;

        public WaveFormat OutputWaveFormat => source.WaveFormat;

        /// <summary>
        /// Closes the WAV file
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

        private CancellationTokenSource pausetoken = new();
        private CancellationTokenSource stoptoken = new();
        private CancellationTokenSource resumetoken = new();

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
                    resumetoken = new();
                    stoptoken = new();
                    pausetoken = new();
                    var t = Task.Run(() =>
                    {
                        unsafe
                        {
                            IntPtr @params = new IntPtr();
                            int dir = 0;
                            WavHeader header = sounddevice.ReadWavHeader(stream);

                            try
                            {
                                sounddevice.OpenPlaybackPcm();
                                sounddevice.PcmInitialize(sounddevice._playbackPcm, header, ref @params, ref dir);
                                Debug.WriteLine("Init finished");

                                ulong frames, bufferSize;

                                int* dirP = &dir;

                                sounddevice._errorNum = Interop.snd_pcm_hw_params_get_period_size(@params, &frames, dirP);
                                sounddevice.ThrowIfError("Can not get period size.");

                                bufferSize = frames * header.BlockAlign;
                                // In Interop, the frames is defined as ulong. But actucally, the value of bufferSize won't be too big.
                                byte[] readBuffer = new byte[(int)bufferSize];

                                fixed (byte* buffer = readBuffer)
                                {
                                    while (stream.Read(readBuffer, 0, readBuffer.Length) != 0)
                                    {
                                        sounddevice._errorNum = Interop.snd_pcm_writei(sounddevice._playbackPcm, (IntPtr)buffer, frames);
                                        sounddevice.ThrowIfError("Can not write data to the device.");
                                    }
                                }
                                Debug.WriteLine("WriteStream finished");
                            
                                //sounddevice.ClosePlaybackPcm();
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
                    t.GetAwaiter();
                        PlaybackState = PlaybackState.Stopped;
                    PlaybackStopped.Invoke(this, new StoppedEventArgs());
                });
            }
        }

        public void Stop()
        {
        if(PlaybackState == PlaybackState.Playing || PlaybackState==PlaybackState.Paused)
        {
            Debug.WriteLine("Stopping");
            Interop.snd_pcm_close(sounddevice._playbackPcm);
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this,new(){});
        }
        }

        public void Pause()
        {
            Debug.WriteLine("Pausing");

            PlaybackState = PlaybackState.Paused;
            Interop.snd_pcm_pause(sounddevice._playbackPcm, 1);
        }

        private WaveStream provider;

        public void Init(IWaveProvider waveProvider)
        {
            source = waveProvider;
            var s = new SoundConnectionSettings();
            s.RecordingBitsPerSample = 16;
            sounddevice = new UnixSoundDevice(s);
            stream = new MemoryStream();
            WaveFileWriter.WriteWavFileToStream(stream, waveProvider);
            stream.Position = 0;
        }
    }
}
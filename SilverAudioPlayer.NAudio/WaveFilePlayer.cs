using Audioz_playerZ;
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;
using PlaybackState = NAudio.Wave.PlaybackState;

namespace SilverAudioPlayer.NAudio
{
    [Export(typeof(IPlay))]
    public class WaveFilePlayer : IPlay, IDisposable, IPlayStreamsToo
    {
        private IWavePlayer? outputDevice;
        public WaveStream? audioFile;
        public byte Volume { get; set; } = 70;
        public string? Decoder { get; private set; } = null;

        public WaveFilePlayer()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                outputDevice = new WaveOutEvent();
            }
            else
            {
                outputDevice = new Unix.PlayProviderExtension.Naudio.ASound.AlsaOutput();
            }
            outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
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

        public void LoadStream(WrappedStream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

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
            {
                outputDevice = new WaveOutEvent
                {
                    Volume = Volume / 100f
                };
            }
            else
            {
                outputDevice.Stop();
            }
            outputDevice.Init(audioFile);
        }

        public void Play()
        {
            outputDevice?.Play();
        }

        public virtual void Stop()
        {
            outputDevice?.Stop();
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
            if (outputDevice != null)
            {
                outputDevice.Volume = volume / 100f;
            }
        }

        public TimeSpan GetPosition()
        {
            if (outputDevice != null && audioFile is WaveStream ws)
            {
                return ws.CurrentTime;
            }
            //this should not happen
            return TimeSpan.Zero;
        }

        public void SetPosition(TimeSpan position)
        {
            if (outputDevice != null && audioFile is WaveStream ws)
            {
                ws.SetPosition(position);
            }
        }

        public Shared.PlaybackState? GetPlaybackState()
        {
            return (outputDevice?.PlaybackState) switch
            {
                PlaybackState.Stopped => Shared.PlaybackState.Stopped,
                PlaybackState.Playing => Shared.PlaybackState.Playing,
                PlaybackState.Paused => Shared.PlaybackState.Paused,
                _ => throw new NotImplementedException(),
            };
        }

        public TimeSpan? Length()
        {
            return ((WaveStream?)audioFile)?.TotalTime;
        }

        public uint? ChannelCount()
        {
            if (audioFile != null)
            {
                return (uint?)audioFile.WaveFormat.Channels;
            }

            return null;
        }

        public long? GetSampleRate()
        {
            if (audioFile != null)
            {
                return audioFile.WaveFormat.SampleRate;
            }

            return null;
        }

        public uint? GetBitsPerSample()
        {
            if (audioFile != null)
            {
                return (uint?)audioFile.WaveFormat.BitsPerSample;
            }

            return null;
        }

        public void Dispose()
        {
            if (outputDevice != null)
            {
                outputDevice.Dispose();
                outputDevice = null;
            }
        }

        public event EventHandler<object> TrackEnd;

        public event EventHandler<object> TrackPause;
    }
}
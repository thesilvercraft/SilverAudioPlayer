using SilverAudioPlayer.Shared;
using CSCore;
using CSCore.Codecs.AIFF;
using CSCore.Codecs.FLAC;
using CSCore.Codecs.WAV;
#if FFMPEG
using CSCore.Ffmpeg;
#endif
using CSCore.SoundOut;
using SilverMagicBytes;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;

namespace SilverAudioPlayer.Any.PlayProvider.CSCore
{
    public class CSCorePlayer : IPlay
    {
        private ISoundOut? _soundOut;
        private IWaveSource? _waveSource;
        public bool DoFunny { get; set; } = false;
        public byte Volume { get; set; } = 70;
        public void Play()
        {
            _soundOut?.Play();
        }

        public void Stop()
        {
            _soundOut?.Stop();
            if (DoFunny) return;
            _soundOut?.Dispose();
            _waveSource?.Dispose();
        }

        public void Pause()
        {
            _soundOut?.Pause();
            TrackPause?.Invoke(null, this);
        }

        public void Resume()
        {
            _soundOut?.Play();
        }

        public void SetVolume(byte volume)
        {
            Volume = volume;
            if (_soundOut != null) _soundOut.Volume = volume / 100f;
        }

        public TimeSpan GetPosition()
        {
            return _waveSource.GetPosition();
        }

        public void SetPosition(TimeSpan position)
        {
            _waveSource.SetPosition(position);
        }

        public PlaybackState? GetPlaybackState() =>
            _soundOut?.PlaybackState switch
            {
                global::CSCore.SoundOut.PlaybackState.Stopped => PlaybackState.Stopped,
                global::CSCore.SoundOut.PlaybackState.Playing => PlaybackState.Playing,
                global::CSCore.SoundOut.PlaybackState.Paused => PlaybackState.Paused,
                _ => throw new NotSupportedException()
            };

        public TimeSpan? Length()
        {
            return _waveSource?.GetLength();
        }

        public uint? ChannelCount()
        {
            return (uint?)_waveSource?.WaveFormat?.Channels;
        }

        public long? GetSampleRate()
        {
            return _waveSource?.WaveFormat?.SampleRate;
        }

        public uint? GetBitsPerSample()
        {
            return (uint?)_waveSource?.WaveFormat?.BitsPerSample;
        }

        public event EventHandler<object> TrackEnd;

        public event EventHandler<object> TrackPause;

        public void LoadStream(WrappedStream stream)
        {
            if (stream.MimeType == KnownMimes.FLACMime)
            {
                _waveSource = new FlacFile(stream.GetStream());
            }
            else if (stream.MimeType == KnownMimes.WAVMime)
            {
                _waveSource = new WaveFileReader(stream.GetStream());
            }
            else if (stream.MimeType == KnownMimes.AiffMime)
            {
                _waveSource = new AiffReader(stream.GetStream());
            }
            #if FFMPEG
            else
            {
                _waveSource= new FfmpegDecoder(stream.GetStream());
            }
            #else 
            else if (stream.MimeType == KnownMimes.MP3Mime || stream.MimeType == KnownMimes.Mp2Mime ||
                     stream.MimeType == KnownMimes.MpegMime)
            {
                _waveSource = new NLayerReader(stream.GetStream());
            }
#endif
            
            if (_soundOut == null)
            {
                _soundOut = new ALSoundOut() { Latency = 100 };
                _soundOut.Stopped += OutputDeviceOnPlaybackStopped;
            }

            _soundOut.Initialize(_waveSource);
        }

        private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            TrackEnd?.Invoke(sender, e);
        }
    }
}
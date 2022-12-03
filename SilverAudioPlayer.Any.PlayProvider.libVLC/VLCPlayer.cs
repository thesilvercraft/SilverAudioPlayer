using LibVLCSharp.Shared;
using SilverAudioPlayer.Shared;
using System.Diagnostics;

namespace SilverAudioPlayer.Any.PlayProvider.libVLC
{
    public class VLCPlayer : IPlay
    {
        public MediaPlayer mp;
        public VLCPlayer(MediaPlayer mp)
        {
            this.mp = mp;
            mp.EndReached += Mp_EndReached;
        }

        private void Mp_EndReached(object? sender, EventArgs e)
        {
            TrackEnd?.Invoke(sender, e);
        }

        public event EventHandler<object> TrackEnd;
        public event EventHandler<object> TrackPause;

        public uint? ChannelCount()
        {
            return null;
        }

        public uint? GetBitsPerSample()
        {
            return null;
        }

        public PlaybackState? GetPlaybackState()
        {
            return mp.State switch
            {
                VLCState.NothingSpecial => PlaybackState.Stopped,
                VLCState.Opening => PlaybackState.Buffering,
                VLCState.Buffering => PlaybackState.Buffering,
                VLCState.Playing => PlaybackState.Playing,
                VLCState.Paused => PlaybackState.Paused,
                VLCState.Stopped => PlaybackState.Stopped,
                VLCState.Ended => PlaybackState.Stopped,
                VLCState.Error => PlaybackState.Stopped,
                _ => PlaybackState.Stopped,
            };
        }

        public TimeSpan GetPosition()
        {
            return TimeSpan.FromMilliseconds(mp.Position * mp.Length);
        }

        public long? GetSampleRate()
        {
            return null;
        }

        public TimeSpan? Length()
        {
            if(mp.Length == -1)
            {
                mp.Play();
                mp.Pause();
            }
            while(mp.Length<=0)
            {
                Thread.Sleep(10);
            }
            return TimeSpan.FromMilliseconds( mp.Length);
        }

        public void Pause()
        {
            mp.Pause();
            TrackPause?.Invoke(this, EventArgs.Empty);
        }

        public void Play()
        {
            mp.Play();
        }

        public void Resume()
        {
            mp.Play();
        }

        public void SetPosition(TimeSpan position)
        {
            mp.Position= ((float)position.TotalMilliseconds) / mp.Length;
        }

        public void SetVolume(byte volume)
        {
            mp.Volume= volume;
        }

        public void Stop()
        {
            Task.Run(() => mp.Stop());  
        }
    }

}
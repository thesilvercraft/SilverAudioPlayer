using SilverAudioPlayer.Shared;
using System.Diagnostics;

namespace SilverAudioPlayer.SystemMediaSoundPlayer
{
    // PLEASE PLEASE PLEASE IN THE NAME OF ANYTHING DONT USE THIS PLAYER AS SOMETHING THAT IS NOT JUST A DEMO OF HOW TO MAKE A PLAYER PLEASE OK THANKS
    public class SystemMediaSoundPlayerwrapper : IPlay, IDisposable
    {
        private System.Media.SoundPlayer player;

        public event EventHandler<object> TrackEnd;

        public event EventHandler<object> TrackPause;

        private long currentPos;

        public static bool CanPlayFile(string URI)
        {
            if (Path.GetExtension(URI).ToUpperInvariant() == ".WAV" && File.Exists(URI))
            {
                using var bytes = File.OpenRead(URI);
                byte[] vs = new byte[4];
                return bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x52 && vs[1] == 0x49 && vs[2] == 0x46 && vs[3] == 0x46 && bytes.Read(vs, 0, 4) == 4 && bytes.Read(vs, 0, 4) == 4 && vs[0] == 0x57 && vs[1] == 0x41 && vs[2] == 0x56 && vs[3] == 0x45;
            }
            return false;
        }

        public uint? ChannelCount()
        {
            //throw new NotImplementedException();
            return null;
        }

        public uint? GetBitsPerSample()
        {
            //throw new NotImplementedException();
            return null;
        }

        public PlaybackState? GetPlaybackState()
        {
            //throw new NotImplementedException();
            return null;
        }

        public TimeSpan GetPosition()
        {
            //throw new NotImplementedException();
            return TimeSpan.Zero;
        }

        public long? GetSampleRate()
        {
            //throw new NotImplementedException();
            return null;
        }

        public TimeSpan? Length()
        {
            //throw new NotImplementedException();
            return null;
        }

        public void LoadFile(string file)
        {
            player = new System.Media.SoundPlayer();
            player.Stream = File.OpenRead(file);
        }

        public void Pause()
        {
            currentPos = player.Stream.Position;
            player.Stop();
        }

        public void Play()
        {
            player.Play();
        }

        public void Resume()
        {
            player.Play();
            player.Stream.Position = currentPos;
            //Does not even work
        }

        public void SetPosition(TimeSpan position)
        {
            //throw new NotImplementedException();
        }

        public void SetVolume(byte volume)
        {
            //throw new NotImplementedException();
        }

        public void Stop()
        {
            currentPos = 0;
            player.Stop();
            Dispose();
        }

        public void Dispose()
        {
            player.Stream.Close();
            player.Stream.Dispose();
            player?.Dispose();
        }
    }
}
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using SilverAudioPlayer.Shared;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.DryWetMidi
{
    [Export(typeof(IPlayProvider))]
    internal class MidiPlayer : IPlay, IPlayProvider
    {
        public event EventHandler<object> TrackEnd;

        public event EventHandler<object> TrackPause;

        private OutputDevice midiOut;
        private MidiFile? mf;
        private PlaybackState? ps;
        private Playback player;

        public MidiPlayer(int deviceNo)
        {
            midiOut = OutputDevice.GetByIndex(deviceNo);
        }

        public MidiPlayer()
        {
        }

        public uint? ChannelCount()
        {
            return (uint?)mf.GetChannels().Count();
        }

        public PlaybackState? GetPlaybackState()
        {
            if (player?.IsRunning == true)
            {
                return PlaybackState.Playing;
            }
            return ps;
        }

        public TimeSpan GetPosition()
        {
            return player.GetCurrentTime<MetricTimeSpan>();
        }

        public TimeSpan? Length()
        {
            return player.GetDuration<MetricTimeSpan>();
        }

        public void Pause()
        {
            ps = PlaybackState.Paused;
            midiOut.TurnAllNotesOff();
            player.Stop();
        }

        public void Play()
        {
            ps = PlaybackState.Playing;
            player.Start();
        }

        public void Resume()
        {
            if (ps == PlaybackState.Paused)
            {
                ps = PlaybackState.Playing;
                player.Start();
            }
        }

        public void SetPosition(TimeSpan position)
        {
            player.MoveToTime(new MetricTimeSpan(position));
        }

        private static Melanchall.DryWetMidi.Common.SevenBitNumber E(Melanchall.DryWetMidi.Common.SevenBitNumber e, int a)
        {
            if (e * (a * 0.01f) < 0)
            {
                return (Melanchall.DryWetMidi.Common.SevenBitNumber)1;
            }
            return (Melanchall.DryWetMidi.Common.SevenBitNumber)(e * (a * 0.01f));
        }

        public void SetVolume(byte volume)
        {
            player.NoteCallback = (rawNoteData, rawTime, rawLength, playbackTime) =>
            new NotePlaybackData(
                 rawNoteData.NoteNumber, // leave note number as is
                E(rawNoteData.Velocity, volume),     // change velocity
                 E(rawNoteData.OffVelocity, volume),  // change off velocity
                 rawNoteData.Channel);     // leave channel as is
        }

        public void Stop()
        {
            ps = PlaybackState.Stopped;
            player.Stop();
            player.Dispose();
            midiOut.Dispose();
            midiOut = null;
        }

        public long? GetSampleRate()
        {
            return null;
        }

        public uint? GetBitsPerSample()
        {
            return null;
        }

       

        public bool CanPlayStream(Stream stream)
        {
            return false;
        }

        public IPlay? GetPlayer(string URI)
        {
            LoadFile(URI);
            return this;
        }

        public void LoadFile(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException(nameof(file));
            }
            midiOut ??= OutputDevice.GetByIndex(0);
            mf = MidiFile.Read(file);
            player = mf.GetPlayback();
            player.Finished += (a, b) => { ps = PlaybackState.Stopped; TrackEnd?.Invoke(a, b); };
            player.Stopped += (a, b) => TrackPause?.Invoke(a, b);
            player.OutputDevice = midiOut;
        }

        public bool CanPlayFile(WrappedStream stream)
        {
            return stream.MimeType == "audio/mid" || stream.MimeType == "audio/midi" || stream.MimeType == ".midi" || stream.MimeType == ".mid" || stream.MimeType == "mid" || stream.MimeType == "midi";
        }

        public IPlay? GetPlayer(WrappedStream stream)
        {
            midiOut ??= OutputDevice.GetByIndex(0);
            mf = MidiFile.Read(stream.RegenStream());
            player = mf.GetPlayback();
            player.Finished += (a, b) => { ps = PlaybackState.Stopped; TrackEnd?.Invoke(a, b); };
            player.Stopped += (a, b) => TrackPause?.Invoke(a, b);
            player.OutputDevice = midiOut;
            return this;
        }

        public Task OnStartup()
        {
            return Task.CompletedTask;
        }
    }
}
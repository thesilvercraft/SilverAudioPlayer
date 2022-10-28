using System.Composition;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.DryWetMidi;

[Export(typeof(IPlayProvider))]
internal class MidiPlayer : IPlay, IPlayProvider
{
    private MidiFile? mf;

    private OutputDevice midiOut;
    private Playback player;
    private PlaybackState? ps;


    public MidiPlayer(int deviceNo)
    {
        midiOut = OutputDevice.GetByIndex(deviceNo);
    }

    public MidiPlayer()
    {
    }

    public event EventHandler<object> TrackEnd;

    public event EventHandler<object> TrackPause;

    public uint? ChannelCount()
    {
        return (uint?)mf.GetChannels().Count();
    }

    public PlaybackState? GetPlaybackState()
    {
        if (player?.IsRunning == true) return PlaybackState.Playing;
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
        midiOut.TurnAllNotesOff();
        player.MoveToTime(new MetricTimeSpan(position));
    }

    public void SetVolume(byte volume)
    {
        player.NoteCallback = (rawNoteData, rawTime, rawLength, playbackTime) =>
            new NotePlaybackData(
                rawNoteData.NoteNumber, // leave note number as is
                E(rawNoteData.Velocity, volume), // change velocity
                E(rawNoteData.OffVelocity, volume), // change off velocity
                rawNoteData.Channel); // leave channel as is
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

    public IPlayProviderListner ProviderListner
    {
        set => _ = value;
    }

    public string Name => "DryWetMidi MidiPlayer";

    public string Description =>
        "A player that plays MIDIs implemented with DryWetMidi (https://github.com/melanchall/drywetmidi)";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(MidiPlayer).Assembly,
        "SilverAudioPlayer.Any.PlayProvider.DryWetMidi.DryWetMidiLogo.png");

    public Version? Version => typeof(MidiPlayer).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links => new()
    {
        new Tuple<Uri, URLType>(
            new Uri("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.DryWetMidi"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/Melanchall.DryWetMidi/{typeof(OutputDevice).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/melanchall/drywetmidi"), URLType.LibraryCode)
    };

    public string Licenses => @"DryWetMidi (https://github.com/melanchall/drywetmidi)
MIT License

Copyright (c) 2018 Maxim Dobroselsky

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
SilverAudioPlayer.Any.PlayProvider.DryWetMidi
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.";

    public IReadOnlyList<MimeType>? SupportedMimes => new List<MimeType> { KnownMimes.MidMime };

    public bool CanPlayFile(WrappedStream stream)
    {
        return stream.MimeType == KnownMimes.MidMime;
    }

    public IPlay? GetPlayer(WrappedStream stream)
    {
        midiOut ??= OutputDevice.GetByIndex(0);
        mf = MidiFile.Read(stream.GetStream());
        player = mf.GetPlayback();
        player.Finished += (a, b) =>
        {
            ps = PlaybackState.Stopped;
            TrackEnd?.Invoke(a, b);
        };
        player.Stopped += (a, b) => TrackPause?.Invoke(a, b);
        player.OutputDevice = midiOut;
        return this;
    }

    public Task OnStartup()
    {
        return Task.CompletedTask;
    }

    private static SevenBitNumber E(SevenBitNumber e, int a)
    {
        if (e * (a * 0.01f) < 0) return (SevenBitNumber)1;
        return (SevenBitNumber)(e * (a * 0.01f));
    }


    public IPlay? GetPlayer(string URI)
    {
        LoadFile(URI);
        return this;
    }

    public void LoadFile(string file)
    {
        if (string.IsNullOrEmpty(file)) throw new ArgumentNullException(nameof(file));
        midiOut ??= OutputDevice.GetByIndex(0);
        mf = MidiFile.Read(file);
        player = mf.GetPlayback();
        player.Finished += (a, b) =>
        {
            ps = PlaybackState.Stopped;
            TrackEnd?.Invoke(a, b);
        };
        player.Stopped += (a, b) => TrackPause?.Invoke(a, b);
        player.OutputDevice = midiOut;
    }
}

using System.Composition;
using Commons.Music.Midi;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
namespace SilverAudioPlayer.Any.PlayProvider.ManagedMidi;

[Export(typeof(IPlayProvider))]
internal class SMidiPlayer : IPlay, IPlayProvider
{
    private MidiMusic? mf;

    private IMidiOutput midiOut;
    private MidiPlayer? player;
    private PlaybackState? ps;


    private bool Enabled = false;
    public SMidiPlayer()
    {
        var access = MidiAccessManager.Default;
        if (access.Outputs.All(x => x.Name.Contains("Midi Through"))) return;
        {
            Enabled = true;
            midiOut = access.OpenOutputAsync(access.Outputs.First(x=>!x.Name.Contains("Midi Through")).Id).Result;
        }
    }

    public event EventHandler<object> TrackEnd;

    public event EventHandler<object> TrackPause;

    public uint? ChannelCount()
    {
        return (uint?)mf.Tracks.Count;
    }

    public PlaybackState? GetPlaybackState()
    {
        return player?.State switch
        {
            PlayerState.Paused => PlaybackState.Paused,
            PlayerState.Stopped => PlaybackState.Stopped,
            PlayerState.Playing => PlaybackState.Playing,
            _ => PlaybackState.Stopped
        };
    }

    public TimeSpan GetPosition()
    {
        return player.PositionInTime;
    }

    public TimeSpan? Length()
    {
        return TimeSpan.FromMilliseconds( player.GetTotalPlayTimeMilliseconds());
    }

    public void Pause()
    {
        if (ps != PlaybackState.Playing) return;
        ps = PlaybackState.Paused;
        player.Pause();
    }

    public void Play()
    {
        if (ps == PlaybackState.Playing) return;
        ps = PlaybackState.Playing;
        player.Play();
    }

    public void Resume()
    {
        if (ps != PlaybackState.Paused) return;
        ps = PlaybackState.Playing;
        player.Play();
    }

    public void SetPosition(TimeSpan position)
    {
    }

    public void SetVolume(byte volume)
    {
    }

    public void Stop()
    {
        if (ps == PlaybackState.Stopped) return;
        ps = PlaybackState.Stopped;
        player?.Stop();
    
    }

    public long? GetSampleRate()
    {
        return null;
    }

    public uint? GetBitsPerSample()
    {
        return null;
    }

 
    public string Name => "managed-midi MidiPlayer";

    public string Description =>
        "A player that plays MIDIs implemented with managed-midi (https://github.com/atsushieno/managed-midi)";

    public WrappedStream? Icon =>null;

    public Version? Version => typeof(MidiPlayer).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.PlayerProvider.ManagedMidi"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/managed-midi/{typeof(MidiPlayer).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/atsushieno/managed-midi"), URLType.LibraryCode)
    ];

    public string Licenses => @"managed-midi (https://github.com/atsushieno/managed-midi)
MIT License

Copyright (c) 2010 Atsushi Eno

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
SilverAudioPlayer.Win.PlayProvider.MidiPlayer
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

    public bool IsTrackLoaded => ps!=PlaybackState.Stopped;

    public bool CanPlayFile(WrappedStream stream)
    {
        return Enabled && stream.MimeType == KnownMimes.MidMime;
    }

    public IPlay? GetPlayer(WrappedStream stream, IPlayerEnvironment? playerEnvironment)
    {
        midiOut ??= MidiAccessManager.Default.OpenOutputAsync(MidiAccessManager.Default.Outputs.First().Id).Result;
         mf = MidiMusic.Read(stream.GetStream());
         
        player = new MidiPlayer(mf, midiOut);
        player.Finished += () =>
        {
            ps = PlaybackState.Stopped;
            TrackEnd?.Invoke(this, this);
        };
        ps=PlaybackState.Paused;
       // player. += (a, b) => TrackPause?.Invoke(a, b);
        return this;
    }

    public Task OnStartup()
    {
        return Task.CompletedTask;
    }


    public void Dispose()
    {
        player?.Dispose();
        //midiOut.Dispose();
        player = null;
        // midiOut = null;
    }
}
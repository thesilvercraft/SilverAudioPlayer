using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;
using SilverMagicBytes;

namespace SilverAudioPlayer.Console;

public class ConsoleEnviroment : IHaveConfigFilesWithKnownLocations, IFullPlayerEnvironment
{

    public event EventHandler<Song> TrackChangedNotification;
    public event EventHandler<PlaybackState> PlayerStateChanged;

    public ConsoleEnviroment(Logic<Context> Logic)
    {
        this.Logic = Logic;
        this.context = Logic.PlayerContext;
    }
    Context context;
    Logic<Context> Logic;
    public void LoadSong(WrappedStream s)
    {
        Task.Run(() => Logic.ProcessStream(s));
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        Logic.ProcessFiles(files);
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        Logic.ProcessStreams(streams);
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return Logic.FilterFiles(files);
    }

    public string Name => "SilverAudioPlayer.Consol";

    public string Description => "console UI for SilverAudioPlayer";

    public WrappedStream? Icon => null;

    public Version? Version => typeof(ConsoleEnviroment).Assembly.GetName().Version;

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Console"),
            URLType.Code),
    ];

    public Task<IMetadata?>? GetMetadataAsync(WrappedStream stream)
    {
        return Logic.GetMetadataFromStream(stream);
    }


    public async Task<List<Song>?> GetQueue()
    {
        //TODO ASK USER BEFORE GIVING OVER QUEUE
        return Logic.GetQueueCopy();
    }

    public IPicture? GetBestRepresentation(IReadOnlyList<IPicture>? pictures, PictureType type = PictureType.Front)
    {
        var res = pictures?.Where(x => x.PicType == type);
        return res?.Any() == true ? res?.First() : pictures?.FirstOrDefault();
    }

    public void Play() => Logic.Play();

    public void Pause() => Logic.Pause();

    public void PlayPause() => Logic.PlayPause(true);

    public void Stop() => Logic.RemoveTrack();

    public void Next() => Logic.Next();

    public void Previous() => Logic.Previous();

    public void SetVolume(byte volume) => context.Volume = volume;

    public byte GetVolume() => context.Volume;

    public Song? GetCurrentTrack()
    {
        return context.CurrentSong;
    }
    public IDictionary<string, string>? GetEnteriesForCurrentTrack()
    {
        return context?.CurrentSong?.Metadata?.Enteries;
    }

    public double GetDuration() => Logic.Player?.Length()?.TotalSeconds ??
                                   (context.CurrentSong?.Metadata?.Duration / 1000) ?? 2;


    public void SetPosition(double position) => Logic.Player?.SetPosition(TimeSpan.FromSeconds(position));

    public double GetPosition() => Logic.Player?.GetPosition().TotalSeconds ?? 1;

    public PlaybackState GetState()
    {
        var x = Logic.Player?.GetPlaybackState();
        if (x != null)
        {
            return (PlaybackState)x;
        }

        return PlaybackState.Stopped;
    }

    public RepeatState GetRepeat() => context.LoopType;

    public void SetRepeat(RepeatState state) => context.LoopType = state;

    public bool GetShuffle()
    {
        return false;
    }

    public void SetShuffle(bool shuffle)
    {
    }

    public void SetRating(byte rating)
    {
        //A music player should probably not edit the metadata of the music it plays
        //If someone thinks otherwise feel free to add code to this method
    }


    public string GetLyrics()
    {
        return context.CurrentSong?.Metadata?.Lyrics;
    }

    public void FireTrackChangedNotification(Song? currentSong)
    {
        Task.Run(() => { TrackChangedNotification?.Invoke(this, currentSong); });
    }

    public void FirePlayerStateChanged(PlaybackState state)
    {
        Task.Run(() => { PlayerStateChanged?.Invoke(this, state); });
    }


    public string Licenses => @"SilverAudioPlayer.Console
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
";

    public string[] KnownConfigFileLocations =>
    [
        ConfigPath.GetPath("SilverAudioPlayer.Config.xml"),
    ];

    public IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements)
    {
        // EkonrensisWindow window = new();
        //window.HandleConfiguration(elements);
        return null;
    }

    public Task<IReadOnlyList<MimeType>> SupportedMimeTypes() => Task.FromResult<IReadOnlyList<MimeType>>(Logic.PlayableMimes);
}
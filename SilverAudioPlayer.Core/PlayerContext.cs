using ReactiveUI;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class PlayerContext : ReactiveObject
{
    private Song? _CurrentSong;
    public RepeatState _LoopType;
    public byte _Volume = 50;
    public Func<RepeatState>? GetLoopType;
    public Func<byte>? GetVolume;
    public Action? HandleLateStageMetadataAndScrollBar = null;
    public Action? ResetUIScrollBar = null;
    public Action<RepeatState>? SetLoopType;
    public Action<string, string>? ShowMessageBox;
    public Action<TimeSpan> SetScrollBarTextTo = null;
    public Action<byte>? VolumeChanged;
    public Func<IList<Song>>? GetQueue;
    public Action<IList<Song>>? SetQueue;

    public byte Volume
    {
        get => GetVolume();
        set => VolumeChanged(value);
    }

    public IList<Song> Queue
    {
        get => GetQueue();
        set => SetQueue(value);
    }

    public Song? CurrentSong
    {
        get => _CurrentSong;
        set
        {
            this.RaiseAndSetIfChanged(ref _CurrentSong, value); // Somehow does not work with ProcessFiles
            _CurrentSong = value;
        }
    }

    public RepeatState LoopType
    {
        get => GetLoopType();
        set => SetLoopType(value);
    }
}
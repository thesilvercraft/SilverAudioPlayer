using ReactiveUI;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using Swordfish.NET.Collections;

namespace SilverAudioPlayer.Console;

public class Context : PlayerContext
{
    private string _Title;
    public ConcurrentObservableCollection<Song> queue = [];

    public string Title
    {
        get => _Title;
        set => this.RaiseAndSetIfChanged(ref _Title, value);
    }

}
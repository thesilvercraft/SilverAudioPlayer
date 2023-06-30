using System.Collections.ObjectModel;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class GuiBinding
{
    public ObservableCollection<WrappedDto> SearchResults { get; set; } = new();
}
using System.Collections.ObjectModel;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class GuiBinding
{
    public void SetResults(ObservableCollection<WrappedDto> wrappedDtos)
    {
        var old = SearchResults;
        SearchResults = wrappedDtos;
        foreach (var wrappedDto in old)
        {
           wrappedDto.Cover?.Dispose();
        }
    }
    public ObservableCollection<WrappedDto> SearchResults { get; private set; } = [];
}
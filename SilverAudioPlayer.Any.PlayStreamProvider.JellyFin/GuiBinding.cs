using System.Collections.ObjectModel;
using ReactiveUI;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class GuiBinding : ReactiveObject
{
    public void SetResults(ObservableCollection<WrappedDto> wrappedDtos)
    {
            var old = SearchResults;
            SearchResults = wrappedDtos;
            if (old == null) return;
            foreach (var wrappedDto in old)
            {
                wrappedDto.Dispose();
            }
    }
    private ObservableCollection<WrappedDto>? _SearchResults;
    
    public ObservableCollection<WrappedDto> SearchResults { get => _SearchResults; private set => this.RaiseAndSetIfChanged(ref _SearchResults, value); }
}
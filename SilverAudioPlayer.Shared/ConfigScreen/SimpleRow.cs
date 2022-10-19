using System.Collections.ObjectModel;

namespace SilverAudioPlayer.Shared.ConfigScreen
{
    public class SimpleRow : IConfigurableRow
    {
        public ObservableCollection<IConfigurableElement> Content { get; set; }
    }
}
using System.Collections.ObjectModel;

namespace SilverAudioPlayer.Shared.ConfigScreen
{
    /// <summary>
    /// A row containing multiple elements stacked horizontally
    /// </summary>
    public interface IConfigurableRow : IConfigurableElement
    {
        public ObservableCollection<IConfigurableElement> Content { get; }

    }
}
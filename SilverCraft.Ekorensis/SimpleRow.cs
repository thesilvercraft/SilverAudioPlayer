using System.Collections.ObjectModel;

namespace SilverCraft.Ekorensis;

public class SimpleRow : IConfigurableRow
{
    public ObservableCollection<IConfigurableElement> Content { get; set; }
}
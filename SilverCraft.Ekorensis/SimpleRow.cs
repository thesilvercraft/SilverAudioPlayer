using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SilverCraft.Ekorensis;

public class SimpleRow : IConfigurableRow
{
    public SimpleRow()
    {
    }

    public SimpleRow(params IConfigurableElement[] elements)
    {
        Content = new ObservableCollection<IConfigurableElement>(elements);
    }

    public SimpleRow(IEnumerable<IConfigurableElement> elements)
    {
        Content = new ObservableCollection<IConfigurableElement>(elements);
    }

    public ObservableCollection<IConfigurableElement> Content { get; set; } = [];
}
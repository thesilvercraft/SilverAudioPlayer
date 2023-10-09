using System.Collections.Generic;

namespace SilverCraft.Ekorensis;

public interface IAmConfigurable
{
    public IEnumerable<IConfigurableElement> GetElements();
}
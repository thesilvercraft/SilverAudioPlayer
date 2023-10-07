using System.Collections.Generic;

namespace SilverCraft.Ekorensis;

public interface IAmConfigurable
{
    public List<IConfigurableElement> GetElements();
}
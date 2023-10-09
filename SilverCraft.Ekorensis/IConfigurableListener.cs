using System.Collections.Generic;

namespace SilverCraft.Ekorensis;

public interface IConfigurableListener
{
    /// <summary>
    /// Try to construct a window based on elements
    /// </summary>
    /// <param name="elements">the elements this window should contain</param>
    /// <returns>either a window or nothing</returns>
    public IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements);
}
using System.Collections.Generic;

namespace SilverCraft.Ekorensis;

public interface IConfigurableGallery : IConfigurableElement
{
    public IEnumerable<IGalleryItem> Items { get; }
    public void DoubleTapped(IGalleryItem item);
}

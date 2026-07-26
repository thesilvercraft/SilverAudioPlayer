using System;
using System.Collections.Generic;

namespace SilverCraft.Ekorensis;

public class SimpleGallery : IConfigurableGallery
{
    public SimpleGallery() { }

    public SimpleGallery(Func<IEnumerable<IGalleryItem>> getItems, Action<IGalleryItem>? onDoubleTapped = null)
    {
        GetItems = getItems;
        OnDoubleTapped = onDoubleTapped;
    }

    public Func<IEnumerable<IGalleryItem>> GetItems { get; set; } = null!;
    public Action<IGalleryItem>? OnDoubleTapped { get; set; }

    public IEnumerable<IGalleryItem> Items => GetItems();
    public void DoubleTapped(IGalleryItem item) => OnDoubleTapped?.Invoke(item);
}

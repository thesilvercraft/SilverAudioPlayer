using System.ComponentModel;

namespace SilverCraft.Ekorensis;

public interface IGalleryItem : INotifyPropertyChanged
{
    public string Title { get; }
    public string Subtitle { get; }
    public string? Image { get; }
}

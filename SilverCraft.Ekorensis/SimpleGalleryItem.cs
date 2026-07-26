using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SilverCraft.Ekorensis;

public class SimpleGalleryItem : IGalleryItem
{
    public SimpleGalleryItem() { }

    public SimpleGalleryItem(string title, string subtitle = "", string? image = null)
    {
        Title = title;
        Subtitle = subtitle;
        Image = image;
    }

    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    
    private string? _image;
    public string? Image
    {
        get => _image;
        set
        {
            _image = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

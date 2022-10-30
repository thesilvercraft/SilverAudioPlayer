using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class AlbumView : UserControl
{
    public AlbumView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
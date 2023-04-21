using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class ServerUrlWindow : Window
{
    private readonly JellyFinHelper jellyFinHelper;

    public string ServerURL;
    public bool Success;

    public ServerUrlWindow()
    {
        InitializeComponent();
        Fail = this.FindControl<TextBlock>("Fail");
        Url = this.FindControl<TextBox>("Url");
#if DEBUG
        
        this.AttachDevTools();
#endif
        this.DoAfterInitTasks(true);
    }

    public ServerUrlWindow(JellyFinHelper jellyFinHelper) : this()
    {
        this.jellyFinHelper = jellyFinHelper;
    }

  

    private async void ButtonClick(object? sender, RoutedEventArgs e)
    {
        Fail.IsVisible = false;
        if (await jellyFinHelper.TryGetSystemInfoAsync(Url.Text))
        {
            ServerURL = Url.Text;
            Success = true;
            Close();
            return;
        }

        Fail.IsVisible = true;
    }
}
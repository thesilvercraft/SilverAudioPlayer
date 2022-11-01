using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SilverAudioPlayer.Avalonia;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class ServerUrlWindow : Window
{
    private readonly JellyFinHelper jellyFinHelper;

    public string ServerURL;
    public bool Success;

    public ServerUrlWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        this.DoAfterInitTasks(true);
    }

    public ServerUrlWindow(JellyFinHelper jellyFinHelper) : this()
    {
        this.jellyFinHelper = jellyFinHelper;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        Fail = this.FindControl<TextBlock>("Fail");
        Url = this.FindControl<TextBox>("Url");
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
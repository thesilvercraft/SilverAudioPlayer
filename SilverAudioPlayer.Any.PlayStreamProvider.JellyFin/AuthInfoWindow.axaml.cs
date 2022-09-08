using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SilverAudioPlayer.Avalonia;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
{
    public partial class AuthInfoWindow : Window
    {
        private JellyFinHelper jellyFinHelper;

        public AuthInfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.DoAfterInitTasks(true);

        }

        public string ServerURL;
        public bool Success = false;

        public AuthInfoWindow(JellyFinHelper jellyFinHelper) : this()
        {
            this.jellyFinHelper = jellyFinHelper;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Fail = this.FindControl<TextBlock>("Fail");
            UserName = this.FindControl<TextBox>("UserName");
            Password = this.FindControl<TextBox>("Password");
        }

        private async void ButtonClick(object? sender, RoutedEventArgs e)
        {
            Fail.IsVisible = false;
            if (await jellyFinHelper.TryLogInAsync(UserName.Text, Password.Text))
            {
                Success = true;
                Close();
                return;
            }

            Fail.IsVisible = true;
        }
    }
}
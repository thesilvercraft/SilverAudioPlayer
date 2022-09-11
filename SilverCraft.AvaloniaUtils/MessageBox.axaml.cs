using Avalonia.Controls;
using SilverAudioPlayer.Avalonia;

namespace SilverCraft.AvaloniaUtils
{
    public partial class MessageBox : Window
    {
        public MessageBox()
        {
            InitializeComponent();
            this.DoAfterInitTasks(true);

        }
        public MessageBox(string Title, string Message):this()
        {
            DataContext = new { Title , Message};

        }

        private void CloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}

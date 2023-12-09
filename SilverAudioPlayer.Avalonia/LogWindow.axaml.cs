using Avalonia.Controls;
using Avalonia.Threading;

namespace SilverAudioPlayer.Avalonia;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        TextBox.Text = App.sink.writer.ToString();
        App.sink.New += (s) => 
            Dispatcher.UIThread.InvokeAsync(()=> TextBox.AppendText(s));
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SilverAudioPlayer.Avalonia;

public partial class ErrorReport : Window
{
    public ErrorReport()
    {
        InitializeComponent();
        TextBox.Text=App.sink.writer.ToString();
    }
}
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Catenoid;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        TextBox.Text = App.sink.writer.ToString();
        App.sink.New += s => Task.Run(() => Dispatcher.UIThread.InvokeAsync(() => TextBox.AppendText(s)));
        this.DoAfterInitTasksF();
    }
}
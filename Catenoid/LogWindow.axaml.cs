using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Catenoid;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        TextBox.Text = App.sink.GetText();
        App.sink.New += OnNewLog;
        this.DoAfterInitTasksF();
    }

    private void OnNewLog(string s)
    {
        Dispatcher.UIThread.Post(() => TextBox.AppendText(s));
    }

    protected override void OnClosed(EventArgs e)
    {
        App.sink.New -= OnNewLog;
        base.OnClosed(e);
    }
}
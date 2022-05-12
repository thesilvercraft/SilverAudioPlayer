using Gtk;
using System;
using System.Diagnostics;
using UI = Gtk.Builder.ObjectAttribute;

namespace SilverAudioPlayer.GTK
{
    internal class MainWindow : Window
    {
        [UI] private Image _pictureBox = null;
        [UI] private Button _playButton = null;
        [UI] private Button _pauseButton = null;
        [UI] private Button _stopButton = null;
        [UI] private ListBox _listBox = null;
        private int _counter;
        private readonly TargetEntry[] targets = new TargetEntry[] { new TargetEntry(null, 0, 0) };

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;
            Drag.DestSet(_listBox, 0, null, 0);
            _listBox.DragBegin += _listBox_DragBegin;
            //_pictureBox.Pixbuf = new Gdk.Pixbuf();
        }

        private void _listBox_DragBegin(object o, DragBeginArgs args)
        {
            Debug.WriteLine(String.Join(' ', args.Args));
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }
    }
}
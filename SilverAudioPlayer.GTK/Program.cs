using Gtk;
using System;

namespace SilverAudioPlayer.GTK
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.Init();

            var app = new Application("org.SilverAudioPlayer.GTK.SilverAudioPlayer.GTK", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            Application.Run();
        }
    }
}

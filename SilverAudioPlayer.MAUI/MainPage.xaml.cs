using SilverAudioPlayer.Shared;
using System.Diagnostics;

namespace SilverAudioPlayer.MAUI
{
    public partial class MainPage : ContentPage
    {
        List<Song> Queue;
        Song CurrentSong;
        public MainPage()
        {
            InitializeComponent();
            Queue = new();
            listView.ItemsSource = Queue;
            gridDropGestureRecogniser.AllowDrop = true;
            gridDropGestureRecogniser.Drop += DropGestureRecognizer_Drop;
        }

       
        private void DropGestureRecognizer_Drop(object sender, DropEventArgs e)
        {
            Debug.WriteLine(e.Data);
        }
    }
}
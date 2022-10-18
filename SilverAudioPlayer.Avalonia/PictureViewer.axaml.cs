using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SilverAudioPlayer.Shared;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.Avalonia
{
    public partial class PictureViewer : Window
    {
        private System.Collections.Generic.IReadOnlyList<Picture>? pictures;

        public PictureViewer()
        {
            InitializeComponent();
            this.DoAfterInitTasks(true);
        }
        DContext x;
        public PictureViewer(System.Collections.Generic.IReadOnlyList<Picture>? pictures) : this()
        {
            this.pictures = pictures;
            using var memstream = new MemoryStream(pictures[0].Data);
            x = new DContext() { Picture = new Bitmap(memstream) };
            DataContext =x;
        }
        int pos = 0;
        private void Left(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if(pos>0)
            {
                pos--;
                using var memstream = new MemoryStream(pictures[pos].Data);
                x.Picture = new Bitmap(memstream);
            }
        }
        private void Right(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (pos < pictures.Count-1)
            {
                pos++;
                using var memstream = new MemoryStream(pictures[pos].Data);
                x.Picture = new Bitmap(memstream);
            }
        }
        private void Copy(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
#if WINDOWS
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                System.Windows.Forms.Clipboard.Clear();
                using var memstream = new MemoryStream(pictures[pos].Data);
                System.Windows.Forms.Clipboard.SetImage(System.Drawing.Image.FromStream(memstream));
            }
#endif

        }
    }
    public class DContext:ReactiveObject
    {
        public Bitmap Picture { get=> picture; set=>this.RaiseAndSetIfChanged(ref picture,value); }
        Bitmap picture;
    }
}

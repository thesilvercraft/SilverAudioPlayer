using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Avalonia;

public partial class PictureViewer : Window
{
    private readonly IReadOnlyList<IPicture>? pictures;
    private int pos;
    private readonly DContext x;

    public PictureViewer()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
    }

    public PictureViewer(IReadOnlyList<IPicture>? pictures) : this()
    {
        this.pictures = pictures;
        using var memstream = new MemoryStream(pictures[0].Data);
        x = new DContext { Picture = new Bitmap(memstream) };
        DataContext = x;
    }

    private void Left(object? sender, RoutedEventArgs e)
    {
        if (pos > 0)
        {
            pos--;
            using var memstream = new MemoryStream(pictures[pos].Data);
            x.Picture = new Bitmap(memstream);
        }
    }

    private void Right(object? sender, RoutedEventArgs e)
    {
        if (pos < pictures.Count - 1)
        {
            pos++;
            using var memstream = new MemoryStream(pictures[pos].Data);
            x.Picture = new Bitmap(memstream);
        }
    }

    private void Copy(object? sender, RoutedEventArgs e)
    {

    }
}

public class DContext : ReactiveObject
{
    private Bitmap picture;

    public Bitmap Picture
    {
        get => picture;
        set => this.RaiseAndSetIfChanged(ref picture, value);
    }
}
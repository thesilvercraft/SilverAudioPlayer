using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Avalonia;

public partial class PictureViewer : Window
{
    private readonly IReadOnlyList<IPicture>? pictures;
    private int pos;
    private readonly DContext x;
    private readonly ILogger? Log;

    public PictureViewer()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
        Log = Logger.GetLogger(typeof(MetadataView));
    }

    public PictureViewer(IReadOnlyList<IPicture>? pictures) : this()
    {
        this.pictures = pictures;
        using var data = pictures[pos].Data.GetStream();
        x = new DContext { Picture = new Bitmap(data) };
        DataContext = x;
    }

    private void Left(object? sender, RoutedEventArgs e)
    {
        if (pos <= 0) return;
        pos--;
        using var data = pictures[pos].Data.GetStream();
        x.Picture = new Bitmap(data);
    }

    private void Right(object? sender, RoutedEventArgs e)
    {
        if (pos >= pictures?.Count - 1) return;
        pos++;
        using var data = pictures[pos].Data.GetStream();
        x.Picture = new Bitmap(data);
    }

    private void Copy(object? sender, RoutedEventArgs e)
    {
        SetClipboardImage();
    }
    public async void SetClipboardImage()
    {
        try
        { 
            MemoryStream m = new();
            await using var data = pictures[pos].Data.GetStream();
            await data.CopyToAsync(m);
            var f = OperatingSystem.IsLinux()
                ? pictures[pos].Data.MimeType.Common
                : pictures[pos].Data.MimeType.FileExtensions[0][1..].ToUpper();
            var bytes =  m.ToArray();
            await Dispatcher.UIThread.InvokeAsync(delegate
            {
                DataObject dataObject = new DataObject();
                dataObject.Set(f, bytes);
                Clipboard?.SetDataObjectAsync(dataObject);
            });
        }
        catch (Exception ex)
        {
            Log?.Error(ex, "Error while setting clipboard image");
        }
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
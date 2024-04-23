using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace Catenoid;

public partial class PictureViewer : Window
{
    private readonly IReadOnlyList<IPicture> pictures = [];
    private int pos;
    private readonly DContext x;
    private readonly ILogger? Log;

    public PictureViewer()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
        x = new DContext { Picture = null }; DataContext = x;
        Log = Logger.GetLogger(typeof(MetadataView));
    }

    public PictureViewer(IReadOnlyList<IPicture>? pictures) : this()
    {
        if (pictures == null)
        {
            Close();
            Log?.Error("The pictureviewer was initialised with NULL pictures???????");
            return;
        }
        this.pictures = pictures;
        if (pictures?.Count < 0)
        {
            Close();
            Log?.Error("The pictureviewer was initialised with no pictures???????");
            return;
        }
        ChangePicture();
    }
    void ChangePicture()
    {
        using var data = pictures[pos].Data?.GetStream();
        if (data != null)
        {
            x.Picture = new Bitmap(data);
        }
    }
    private void Left(object? sender, RoutedEventArgs e)
    {
        if (pos <= 0) return;
        pos--;
        ChangePicture();
    }

    private void Right(object? sender, RoutedEventArgs e)
    {
        if (pos >= pictures?.Count - 1) return;
        pos++;
        ChangePicture();
    }

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        var wrappedStream = pictures[pos].Data;
        if (wrappedStream == null) return;
        await this.SetClipboardImage(wrappedStream, Log);
        SAPAWindowExtensions.Toast(SAPAWindowExtensions.CopiedToClipboard);
    }
}
public class DContext : ReactiveObject
{
    private Bitmap? picture = null;

    public Bitmap? Picture
    {
        get => picture;
        set => this.RaiseAndSetIfChanged(ref picture, value);
    }
}
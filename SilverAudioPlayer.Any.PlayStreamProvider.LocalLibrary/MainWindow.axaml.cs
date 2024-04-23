using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;


public static class ShowableUtils
{
    public static Tuple<Bitmap, RelianceOnShared<Bitmap>>? DecodeCover(IPicture cover)
    {
        try
        {
            if (SharedMemoryTPoolInstance<Bitmap>.Instance.ContainsHash(cover.Hash))
            {
                var b = SharedMemoryTPoolInstance<Bitmap>.Instance.GetWithHash(cover.Hash);
                return new(b.Object, new RelianceOnShared<Bitmap>(b));
            }
            else
            {
                var imageData = cover.Data;
                if (imageData != null)
                {
                    using var imageDataStream = imageData.GetStream();
                    var bmp = new Bitmap(imageDataStream);
                    var b = SharedMemoryTPoolInstance<Bitmap>.Instance.AddT(bmp, cover.Hash);
                    return new(bmp, new RelianceOnShared<Bitmap>(b));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error loading cover");
            Debug.WriteLine(e);
        }
        return null;
    }

}

public class GuiBinding : ReactiveObject
{
    public AvaloniaList<IndexAlbum> WrappedAlbums { get => _WrappedAlbums; set => this.RaiseAndSetIfChanged(ref _WrappedAlbums, value); }
    private AvaloniaList<IndexAlbum> _WrappedAlbums = [];

    public AvaloniaList<IndexSong> WrappedSongs { get => _WrappedSongs; set => this.RaiseAndSetIfChanged(ref _WrappedSongs, value); }
    private AvaloniaList<IndexSong> _WrappedSongs = [];

}

public partial class MainWindow : Window
{
    private IPlayStreamProviderListener Env;
    private GuiBinding _binding = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _binding;
        LB = this.FindControl<ListBox>("LB");
        RB = this.FindControl<ListBox>("RB");
        if (WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks") == true)
        {
            return;
        }
        MainGrid = this.FindControl<Grid>("MainGrid");
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
        this.DoAfterInitTasks(true);
    }
    private void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        Env.LoadSongs(_binding.WrappedSongs.Select(song => new WrappedFileStream(song.Loc)).ToList()); //TODO GET SORTED PROPERLY
    }
    public MainWindow(IMediaIndexer indexer, LocalLibraryPlayStreamProviderConfig value, IPlayStreamProviderListener env) : this()
    {
        config = value;
        Indexer = indexer;
        Env = env;
        _binding.WrappedAlbums = Indexer.Albums;

        /*Task.Run(() =>
        {
            Thread.Sleep(1000);
        });
*/
    }
    IMediaIndexer Indexer;
    LocalLibraryPlayStreamProviderConfig? config;

    private async void LoadNewFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolderDialog ofd = new();
        var f = await ofd.ShowAsync(this);
        if (f == null) return;
        config.Directories.Add(f);
        ((ICanBeToldThatAPartOfMeIsChanged)config).PropertyChanged(config, new("Directories"));
    }

    private void RB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RB.SelectedItem is IndexSong ws)
        {
            Env.LoadSong(new WrappedFileStream(ws.Loc));
        }
    }

    private void LB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LB.SelectedItem is IndexAlbum wa)
        {
            _binding.WrappedSongs = Indexer.GetFromAlbum(wa);
        }

    }
}
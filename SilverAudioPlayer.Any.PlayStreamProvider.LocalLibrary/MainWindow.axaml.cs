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
using SLibIndex;

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

            var imageData = cover.Data;
            if (imageData != null)
            {
                using var imageDataStream = imageData.GetStream();
                var bmp = new Bitmap(imageDataStream);
                var b = SharedMemoryTPoolInstance<Bitmap>.Instance.AddT(bmp, cover.Hash);
                return new(bmp, new RelianceOnShared<Bitmap>(b));
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
    private readonly IPlayStreamProviderListener _env;
    private readonly GuiBinding _binding = new();
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
    public MainWindow(LocalLibraryPlayStreamProviderConfig value, IPlayStreamProviderListener env) : this()
    {
        _config = value;
        _env = env;
        if (File.Exists(IndexedTrack.ExpectedFileLocation))
        {
            try
            {
             _songs = IndexedTrack.LoadFromExpectedLocation();
            }
            catch (Exception e)
            {
                Log.Error(e,"exception in loading ");
            }
        }
        _binding.WrappedAlbums = new();
        var albums = new AvaloniaList<IndexAlbum>();
        foreach (var group in _songs.GroupBy(x => x.Album))
        {
            albums.Add(new()
            {
                Name=group.Key,
                AlbumArtist = group.FirstOrDefault()?.Artist??"",
                ImageLoc =  group.FirstOrDefault()?.Art??"",
                
            });
        }
        _binding.WrappedAlbums = albums;
    }
    private void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        _env.LoadSongs(_binding.WrappedSongs.Select(song => new WrappedFileStream(song.Loc)).ToList()); //TODO GET SORTED PROPERLY
    }
    private readonly List<IndexedTrack> _songs= [];
    private readonly LocalLibraryPlayStreamProviderConfig? _config;

    private async void LoadNewFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = GetTopLevel(this);
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new ()
            {
                Title = "Add new folders to the config",
                AllowMultiple = true
            });
            if (folders is not { Count: > 0 }) return;
            _config?.Directories.AddRange(folders.Select(x=>x.Path.ToString()).Where(Directory.Exists));
            ((ICanBeToldThatAPartOfMeIsChanged?)_config)?.PropertyChanged(_config, new("Directories"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading folder to config in locallib");
        }
    }

    private void RB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RB.SelectedItem is IndexSong ws)
        {
            _env.LoadSong(new WrappedFileStream(ws.Loc));
        }
    }

    private void LB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LB.SelectedItem is IndexAlbum wa)
        {
            _binding.WrappedSongs = new (_songs.Where(x=>x.Album== wa.Name).Select(x=> new IndexSong(x.Location, x, _env)));
        }
    }
}
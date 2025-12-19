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
    private IPlayStreamProviderListener Env;
    private GuiBinding _binding = new();
    public MainWindow()
    {
       // Logger.GetLogger();
        
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
    public MainWindow(LocalLibraryPlayStreamProviderConfig value, IPlayStreamProviderListener env) : this()
    {
        config = value;
        Env = env;
        if (File.Exists(IndexedTrack.ExpectedFileLocation))
        {
            try
            {
             songs = IndexedTrack.LoadFromExpectedLocation();
            }
            catch (Exception e)
            {
                Log.Error(e,"exception in loading ");
            }
        }
        _binding.WrappedAlbums = new();
        var albums = new AvaloniaList<IndexAlbum>();
        foreach (var group in songs.GroupBy(x => x.Album))
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
    private List<IndexedTrack> songs=new();
    LocalLibraryPlayStreamProviderConfig? config;

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
            config?.Directories.AddRange(folders.Select(x=>x.Path.ToString()).Where(Directory.Exists));
            ((ICanBeToldThatAPartOfMeIsChanged?)config)?.PropertyChanged(config, new("Directories"));
        }
        catch (Exception ex)
        {
        }
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
            _binding.WrappedSongs = new (songs.Where(x=>x.Album== wa.Name).Select(x=> new IndexSong(x.Location, x, Env)));
        }
    }
}
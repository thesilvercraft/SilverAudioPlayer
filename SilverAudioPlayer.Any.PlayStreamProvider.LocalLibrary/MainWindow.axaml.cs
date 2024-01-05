using System.Diagnostics;
using System.IO;
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
using Swordfish.NET.Collections;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;

public interface WrappedShowable
{
    public string Name { get; }
    public Bitmap? Cover { get; }
    public string AlbumArtist { get; }
}
public class WrappedAlbum : WrappedShowable
{
    public string Name { get; set; }
    public Bitmap? Cover => Songs.Count>0 ? Songs[0].Cover : null;
    public string AlbumArtist { get; set; }
    public AvaloniaList<WrappedSong> Songs { get; } = [];
}
public class WrappedSong : WrappedShowable, IDisposable
{
    public WrappedSong(IMetadata meta, string url)
    {
        Url = url;
        Metadata = meta;
    }
    public string Url { get; }
    public IMetadata Metadata;
    public string Name => Metadata.Title;
    public int? TrackNumber => Metadata.TrackNumber;
    public Bitmap? Cover =>c??= DecodeCover();
    public Bitmap? c;
    RelianceOnShared<Bitmap> Reliance;

    public Bitmap? DecodeCover()
    {
        try
        {
            if (Metadata.Pictures != null && Metadata.Pictures.Count > 0)
            {
                if(SharedMemoryTPoolInstance<Bitmap>.Instance.ContainsHash(Metadata.Pictures[0].Hash))
                {
                    var b =SharedMemoryTPoolInstance<Bitmap>.Instance.GetWithHash(Metadata.Pictures[0].Hash);
                    Reliance = new(b);
                    return b.Object;

                }
                else
                {
                    var imageData = Metadata.Pictures[0].Data;
                    if (imageData != null)
                    {


                        using var imageDataStream = imageData.GetStream();
                        var bmp = new Bitmap(imageDataStream);
                        var b = SharedMemoryTPoolInstance<Bitmap>.Instance.AddT(bmp, Metadata.Pictures[0].Hash);
                        Reliance = new(b);

                        return bmp;


                    }
                }
              
            }
        }
        catch (Exception e)
        {
            Log.Error(e,"Error loading cover");
            Debug.WriteLine(e);
        }
        return null;
    }

    public string AlbumArtist => Metadata.Artist;

    
   
    public void Dispose()
    {
        Reliance.Dispose();
        GC.SuppressFinalize(this);
    }
}
public class GuiBinding : ReactiveObject
{
    public AvaloniaList<WrappedAlbum> WrappedAlbums { get; set; } = [];
    public AvaloniaList<WrappedSong> WrappedSongs { get => _WrappedSongs; set => this.RaiseAndSetIfChanged(ref _WrappedSongs, value); }
    private AvaloniaList<WrappedSong> _WrappedSongs = [];

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
        Env.LoadSongs(_binding.WrappedSongs.Select(song => new WrappedFileStream(song.Url)).ToList()); //TODO GET SORTED PROPERLY
    }
    public MainWindow(IPlayStreamProviderListener env, LocalLibraryPlayStreamProviderConfig value) : this()
    {
        Env = env;
        config = value;
        foreach (var f in config.Directories)
        {
            Task.Run(()=>LoadFolder(f));
        }
    }
    LocalLibraryPlayStreamProviderConfig? config;
    Dictionary<string, Bitmap> bitmaps = [];
    public async Task<WrappedSong?> ProcessFileAsync(string file, CancellationToken ct = default)
    {
        try
        {
            var meta = await Env.GetMetadataAsync(new WrappedFileStream(file));
            return new WrappedSong(meta, file);
        }
        catch (Exception e)
        {
            Debug.WriteLine(file);
            Log.Error(e,"Error ProcessFileasync in LocalLib");
        }
        return null;
    }
    async void LoadFolder(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        object a = new();
        List<WrappedSong> songs = [];
        await Parallel.ForEachAsync(Env.FilterFiles(files), new ParallelOptions() { MaxDegreeOfParallelism = 5 }, async (file, ct) =>
        {
            try
            {
                var song = await ProcessFileAsync(file, ct);
                if (song != null)
                {
                    lock (a)
                    {
                        songs.Add(song);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in processfile in parrallel foreach");
            }
        });
        var albums = songs.GroupBy(x => x.Metadata.Album);
        foreach (var album in albums.Where(a => a.Key != null))
        {
            if (_binding.WrappedAlbums.FirstOrDefault(x => x.Name == album.Key) is { } wrappedAlbum)
            {
                wrappedAlbum.Songs.AddRange(album.ToList());
            }
            else
            {
                var albuml = album.ToList();
                var wrapped = new WrappedAlbum()
                {
                    AlbumArtist = albuml[0]?.Metadata?.Artist ?? "ERROR",
                    Name = albuml[0]?.Metadata?.Album ?? "ERROR",
                };

                wrapped.Songs.AddRange(album.ToList());
                _binding.WrappedAlbums.Add(wrapped);
            }
        }
    }
    private async void LoadNewFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolderDialog ofd = new();
        var f = await ofd.ShowAsync(this);

        if (f != null)
        {

            LoadFolder(f);
            config.Directories.Add(f);
            ((ICanBeToldThatAPartOfMeIsChanged)config).PropertyChanged(config, new("Directories"));

        }


    }

    private void RB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RB.SelectedItem is WrappedSong ws)
        {
            Env.LoadSong(new WrappedFileStream(ws.Url));
        }
    }

    private void LB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LB.SelectedItem is WrappedAlbum wa)
        {

            _binding.WrappedSongs = wa.Songs;

        }

    }
}
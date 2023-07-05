using System.Collections.ObjectModel;
using System.Composition;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;
using Swordfish.NET.Collections;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;

[Export(typeof(IPlayStreamProvider))]
public class LocalLibraryPlayStreamProvider : IPlayStreamProvider
{
    public void Use(IPlayStreamProviderListener env)
    {
        MainWindow mw = new(env);
        mw.Show();
    }

    public string Name => "Local Library";
    public string Description => "Allows you to sort and play local files";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(LocalLibraryPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary\nGPL3.0";
    public List<Tuple<Uri, URLType>>? Links { get; }
}


public interface WrappedShowable
{
    public string Name { get; }
    public Bitmap? Cover { get; }
    public string AlbumArtist { get; }
}
public class WrappedAlbum : WrappedShowable
{
    public string Name { get; set; }
    public Bitmap? Cover => Songs[0].Cover;
    public string AlbumArtist { get; set; }
    public ConcurrentObservableCollection<WrappedSong> Songs { get; } = new();
}
public class WrappedSong : WrappedShowable
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
    public Bitmap? Cover => DecodeCover();
    public Bitmap? DecodeCover()
    {
        if(Metadata.Pictures.Count>0)
        {
            using var stream = Metadata.Pictures[0].Data.GetStream();
            return Bitmap.DecodeToHeight(stream, 400);
        }
        return null;
    }

    public string AlbumArtist => Metadata.Artist;
}
public class GuiBinding : ReactiveObject
{
    public ConcurrentObservableCollection<WrappedAlbum> WrappedAlbums { get; set; } = new();
    public ConcurrentObservableCollection<WrappedSong> WrappedSongs { get => _WrappedSongs; set => this.RaiseAndSetIfChanged(ref _WrappedSongs, value); }
    private ConcurrentObservableCollection<WrappedSong> _WrappedSongs = new();

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
        this.DoAfterInitTasks(true);
    }
    private void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        Env.LoadSongs(_binding.WrappedSongs.Select(song => new WrappedFileStream(song.Url)).ToList()); //TODO GET SORTED PROPERLY
    }
    public MainWindow(IPlayStreamProviderListener env) : this()
    {
        Env = env;
    }
    Dictionary<string, Bitmap> bitmaps = new();
    public async Task<WrappedSong?> ProcessFileAsync(string file, CancellationToken ct=default)
    {
        try
        {
            var meta = await Env.GetMetadataAsync(new WrappedFileStream(file));
            return new WrappedSong(meta, file);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        return null;
    }
    private async void LoadNewFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolderDialog ofd = new();
        var f = await ofd.ShowAsync(this);
        var restOfTask = Task.Run(async () =>
        {
            if (f != null)
            {
                var files = Directory.GetFiles(f, "*", SearchOption.AllDirectories);
                object a = new();
                List<WrappedSong> songs = new();
                await Parallel.ForEachAsync(Env.FilterFiles(files), new ParallelOptions() { MaxDegreeOfParallelism = 10 }, async (file, ct) => {
                    var song= await ProcessFileAsync(file, ct);
                    if(song!=null)
                    {
                        lock (a)
                        {
                            songs.Add(song);
                        }
                    }
                });
                var albums = songs.GroupBy(x => x.Metadata.Album);
            foreach (var album in albums)
                {
                    if(_binding.WrappedAlbums.FirstOrDefault(x => x.Name == album.Key) is { } wrappedAlbum)
                    {
                        wrappedAlbum.Songs.AddRange(album.ToList());
                    }
                    else
                    {
                        Bitmap? cover = null;
                        var albuml = album.ToList();
                        var wrapped = new WrappedAlbum()
                        {
                            AlbumArtist = albuml[0].Metadata.Artist,
                            Name = albuml[0].Metadata.Album,
                        };
                        wrapped.Songs.AddRange(album.ToList());
                        _binding.WrappedAlbums.Add(wrapped);
                    }
                }
                
            }
        });

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
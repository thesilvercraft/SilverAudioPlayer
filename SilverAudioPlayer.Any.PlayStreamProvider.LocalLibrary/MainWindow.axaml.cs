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
public class WrappedAlbum :WrappedShowable
{
    public string Name { get; set; }
    public Bitmap? Cover { get;  set; }
    public string AlbumArtist { get; set;  }
    public ConcurrentObservableCollection<WrappedSong> Songs { get; } = new();
}
public class WrappedSong :WrappedShowable
{
    public WrappedSong(Metadata meta, string url, ref Dictionary<string, Bitmap> bitmaps)
    {
        if (meta.Pictures is not (null or { Count: 0 }))
        {
            if (!bitmaps.TryGetValue(meta.Pictures.First().Hash, out _Cover))
            {
                _Cover = Bitmap.DecodeToHeight(new MemoryStream(meta.Pictures.First().Data), 400);
                bitmaps.Add(meta.Pictures.First().Hash, _Cover);
            }
        }
        Url = url;
        Metadata = meta;
    }
    public string Url { get; }
    public Metadata Metadata;
    public string Name => Metadata.Title;
    public int? TrackNumber => Metadata.TrackNumber;
    public Bitmap? Cover => _Cover;
    public Bitmap? _Cover;

    public string AlbumArtist => Metadata.Artist;
}
public class GuiBinding :ReactiveObject
{
    public ConcurrentObservableCollection<WrappedAlbum> WrappedAlbums { get; set; } = new();
    public ConcurrentObservableCollection<WrappedSong> WrappedSongs { get=>_WrappedSongs; set=>this.RaiseAndSetIfChanged(ref _WrappedSongs,value); } 
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
        if(WindowExtensions.envBackend.GetBool("SAPDoNotDoInitTasks")==true)
        {
            return;
        }
        this.DoAfterInitTasks(true);
    }
    private void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
   
            Env.LoadSongs(_binding.WrappedSongs.Select(song=>new WrappedFileStream (song.Url)).ToList()); //TODO GET SORTED PROPERLY
    }
    public MainWindow(IPlayStreamProviderListener env) :this()
    {
        Env = env;
    }
    Dictionary<string, Bitmap> bitmaps = new();
    public async Task ProcessFileAsync(string file)
    {
        try
        {
            var meta = await Env.GetMetadataAsync(new WrappedFileStream(file));
            if (meta != null)
            {
                var album=_binding.WrappedAlbums.FirstOrDefault(x => x.Name == meta.Album);
                if (album != null)
                {
                    album.Songs.Add(new WrappedSong(meta,file, ref bitmaps));
                }
                else
                {
                    Bitmap? cover=null;
                    if (meta.Pictures is not (null or { Count: 0 }))
                    {
                        if(!bitmaps.TryGetValue(meta.Pictures.First().Hash, out cover))
                        {
                            cover = Bitmap.DecodeToHeight(new MemoryStream(meta.Pictures.First().Data), 400);
                            bitmaps.Add(meta.Pictures.First().Hash, cover);
                        }
                    }
                    _binding.WrappedAlbums.Add(new()
                    {
                        AlbumArtist = meta.Artist,
                        Cover = cover,
                        Name = meta.Album
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
    private async void LoadNewFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolderDialog ofd = new();
        var f = await ofd.ShowAsync(this);
        if (f != null)
        {
            var files =Directory.GetFiles(f, "*", SearchOption.AllDirectories);
            foreach(var file in Env.FilterFiles(files))
            {
                await ProcessFileAsync(file);

            }

        }
    }

    private void RB_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RB.SelectedItem is WrappedSong ws)
        {
            //Env.LoadSong(new WrappedFileStream(ws.Url));
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
using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Controls.Embedding.Offscreen;
using Avalonia.Media.Imaging;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;
public interface IMediaIndexer
{
    /// <summary>
    /// Get rid of all known indexes
    /// </summary>
    public void Clean();
    /// <summary>
    /// Add a directory to this indexer
    /// </summary>
    /// <param name="Dir">full path to directory</param>
    public Task AddDirectory(string Dir);
    /// <summary>
    /// Refreshes the indexes
    /// </summary>
    /// <param name="force">process already indexed files, or just removed and new</param>
    public Task RefreshIndex(bool force = false);
    public AvaloniaList<IndexAlbum> Albums { get; }
    public AvaloniaList<IndexSong> GetFromAlbum(IndexAlbum showable);
}
public class IndexShowable
{
    public string Name { get; set; }
    public string AlbumArtist { get; set; }
    public Shared.Metadata.IPicture Image { get; set; }

    public Bitmap? Cover =>DecodeBitmap();
    Bitmap? DecodeBitmap()
    {
        if(b!=null)
        {
            return b;
        }
        if(Image!=null)
        {
            var a=ShowableUtils.DecodeCover(Image);
            if(a==null) return null;
            (b, Reliance)=a;
            return b;
        }
        return null;
    }
    Bitmap b;
    RelianceOnShared<Bitmap> Reliance;

}
public class IndexAlbum : IndexShowable
{
}
public class IndexSong : IndexShowable
{
    public IndexSong(string loc, IMetadata metadata,IPlayStreamProviderListener env)
    {
        Loc = loc;
        Album=metadata.Album;
        LastChecked = DateTime.UtcNow;
        Image=metadata.Pictures.FirstOrDefault();
    }
    public string Album{get;set;}
    public string Loc { get; set; }
    public DateTime LastChecked { get; set; }
    public bool HasChanged() => !Exists() || (File.GetLastWriteTimeUtc(Loc) > LastChecked);
    public bool Exists() => File.Exists(Loc);

}
public class LocalLibraryMediaIndexer : IMediaIndexer
{
    public LocalLibraryMediaIndexer(IPlayStreamProviderListener env, LocalLibraryPlayStreamProviderConfig config)
    {
        Env = env;
        Config = config;
        Parallel.ForEachAsync(config.Directories, async (x,c)=>{
            await AddDirectory(x);
        });
    }

    IPlayStreamProviderListener Env;
    LocalLibraryPlayStreamProviderConfig? Config;

    public AvaloniaList<IndexAlbum> Albums = new();
    ConcurrentBag<IndexSong> AllSongs = new();
    ConcurrentDictionary<IndexAlbum, AvaloniaList<IndexSong>> AlbumSongs = new();
    AvaloniaList<IndexAlbum> IMediaIndexer.Albums => Albums;
    public async Task<IndexSong?> ProcessFileAsync(string file, CancellationToken ct = default)
    {
        try
        {
            var meta = await Env?.GetMetadataAsync(new WrappedFileStream(file));
            return new IndexSong(file, meta,Env);
        }
        catch (Exception e)
        {
            Debug.WriteLine(file);
            Log.Error(e, "Error ProcessFileasync in LocalLib");
        }
        return null;
    }

    public async Task AddDirectory(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);

        await Parallel.ForEachAsync(Env.FilterFiles(files), new ParallelOptions() { MaxDegreeOfParallelism = 5 }, async (file, ct) =>
        {
            try
            {
                var song = await ProcessFileAsync(file, ct);
                if (song != null)
                {
                    AllSongs.Add(song);
                    if (Albums.FirstOrDefault(x => x.Name == song.Album) is { } album)
                    {
                        if (AlbumSongs.TryGetValue(album, out AvaloniaList<IndexSong>? value))
                        {
                            value.Add(song);
                        }
                        else
                        {
                            AlbumSongs[album] = [song];
                        }
                    }
                    else
                    {
                        album = new IndexAlbum()
                        {
                            Name = song.Album,
                            AlbumArtist = song.AlbumArtist
                        };
                        Albums.Add(album);
                        AlbumSongs[album] = [song];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in processfile in parrallel foreach");
            }
        });

    }


    public void Clean()
    {
    }

    public AvaloniaList<IndexSong> GetFromAlbum(IndexAlbum showable)
    {
        return AlbumSongs[showable];
    }

    public async Task RefreshIndex(bool force = false)
    {
    }
}
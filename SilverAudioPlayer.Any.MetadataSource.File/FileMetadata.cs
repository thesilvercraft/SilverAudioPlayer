using System.Security.Cryptography;

namespace SilverAudioPlayer.Any.MetadataSource.File;

using SilverAudioPlayer.Shared;
using SilverMagicBytes;

public class FileMetadata : IMetadata
{
    private string FileUrl;
    private string DirUrl;
    public FileMetadata(WrappedFileStream fs)
    {
        FileUrl = fs.URL;
        DirUrl = Path.GetDirectoryName(FileUrl);
        var files= Directory.EnumerateFiles(DirUrl, Path.GetFileNameWithoutExtension(FileUrl) + ".lrc",
            new EnumerationOptions()
                { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true });

       foreach (var possiblelrcfile in files)
       {
           if (!System.IO.File.Exists(possiblelrcfile)) continue;
           try
           {
               var s = global::Opportunity.LrcParser.Lyrics.Parse(System.IO.File.ReadAllText(possiblelrcfile));
               SyncedLyrics = s.Lyrics.Lines.Select(x => new LyricPhrase((int)(x.Timestamp.Ticks / TimeSpan.TicksPerMillisecond), x.Content + "\n")).ToList();
               break;
           }
           catch { }

       }
       var cover= Directory.EnumerateFiles(DirUrl, "cover*",
           new EnumerationOptions()
               { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true});

       foreach (var covr in cover)
       {
           WrappedFileStream wfs = new(covr);
           using var imagetohash= wfs.GetStream();
           imagetohash.Position = 0;
                   
           _internalPictures?.Add(new Picture(wfs));
       }
    }

    public void Dispose()
    {
       
    }
    
    public string? Title { get; }
    public string? Artist { get; }
    public string? Album { get; }
    public string? Genre { get; }
    public int? Year { get; }
    public ulong? Bitrate { get; }
    public ulong? SampleRate { get; }
    public uint? Channels { get; }
    public int? TrackNumber { get; }
    public int? DiscNumber { get; }
    public string[]? Comments { get; }
    public double? Duration { get; }
    public string? Lyrics { get; }
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<IPicture>? _internalPictures = new();
    public IReadOnlyList<IPicture>? Pictures=>_internalPictures;
}


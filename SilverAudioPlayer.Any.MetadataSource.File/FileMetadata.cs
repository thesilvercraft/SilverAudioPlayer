using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.File;

using System.Diagnostics;
using SilverAudioPlayer.Shared;

public class FileMetadata : IMetadata
{
    private string FileUrl;
    private string DirUrl;
    public FileMetadata(WrappedFileStream fs)
    {
        FileUrl = fs.URL;
        DirUrl = Path.GetDirectoryName(FileUrl);
        var files = Directory.EnumerateFiles(DirUrl, Path.GetFileNameWithoutExtension(FileUrl) + ".lrc",
            new EnumerationOptions()
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

        foreach (var possiblelrcfile in files)
        {
            if (!System.IO.File.Exists(possiblelrcfile)) continue;
            try
            {
                var s = Opportunity.LrcParser.Lyrics.Parse(System.IO.File.ReadAllText(possiblelrcfile));
                SyncedLyrics = s.Lyrics.Lines.Select(x => new LyricPhrase((int)(x.Timestamp.Ticks / TimeSpan.TicksPerMillisecond), x.Content + "\n")).ToList();
                break;
            }
            catch { }

        }
        var txtfiles = Directory.EnumerateFiles(DirUrl, Path.GetFileNameWithoutExtension(FileUrl) + ".txt",
            new EnumerationOptions()
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

        foreach (var possibletxtfile in txtfiles)
        {
            if (!System.IO.File.Exists(possibletxtfile)) continue;
            try
            {

                Lyrics = System.IO.File.ReadAllText(possibletxtfile);
                break;
            }
            catch { }

        }
        var cover = Directory.EnumerateFiles(DirUrl, "cover*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }).ToList();
        cover.AddRange(Directory.EnumerateFiles(DirUrl, "front*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }));
        cover.AddRange(Directory.EnumerateFiles(DirUrl, "album*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }));
        foreach (var covr in cover)
        {
            Debug.WriteLine(covr);
            WrappedFileStream wfs = new(covr);
            using var imagetohash = wfs.GetStream();
            imagetohash.Position = 0;
            _internalPictures?.Add(new Picture(wfs));
        }
    }

    public void Dispose()
    {
        if (_internalPictures != null)
        {
            foreach (var picture in _internalPictures)
            {
                picture.Dispose();
            }
        }
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
    private List<IPicture>? _internalPictures = [];
    public IReadOnlyList<IPicture>? Pictures => _internalPictures;
}


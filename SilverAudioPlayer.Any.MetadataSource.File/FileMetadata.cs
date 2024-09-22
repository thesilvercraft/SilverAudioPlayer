using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.File;

using System.Diagnostics;
using SilverAudioPlayer.Shared;

public class FileMetadata : IMetadata
{
    public FileMetadata(WrappedFileStream fs)
    {
        var fileUrl = fs.URL;
        var dirUrl = Path.GetDirectoryName(fileUrl);
        var files = Directory.EnumerateFiles(dirUrl, Path.GetFileNameWithoutExtension(fileUrl) + ".lrc",
            new EnumerationOptions()
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

        foreach (var possibleLrcPath in files)
        {
            if (!System.IO.File.Exists(possibleLrcPath)) continue;
            try
            {
                SyncedLyrics = LrcParser.Parse(System.IO.File.ReadAllText(possibleLrcPath));
                break;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
        var txtFiles = Directory.EnumerateFiles(dirUrl, Path.GetFileNameWithoutExtension(fileUrl) + ".txt",
            new EnumerationOptions()
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

        foreach (var possibleTxtFile in txtFiles)
        {
            if (!System.IO.File.Exists(possibleTxtFile)) continue;
            try
            {
                Lyrics = System.IO.File.ReadAllText(possibleTxtFile);
                break;
            }
            catch
            {
                // ignored
            }
        }
        var cover = Directory.EnumerateFiles(dirUrl, "cover*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }).ToList();
        cover.AddRange(Directory.EnumerateFiles(dirUrl, "front*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }));
        cover.AddRange(Directory.EnumerateFiles(dirUrl, "album*",
           new EnumerationOptions()
           { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false, IgnoreInaccessible = true, MaxRecursionDepth = 2 }));
        foreach (var covr in cover)
        {
            Debug.WriteLine(covr);
            WrappedFileStream wfs = new(covr);
            if (wfs.MimeType is not SilverMagicBytes.ImageMime) continue;
            using var imageToHash = wfs.GetStream();
            imageToHash.Position = 0;
            _internalPictures?.Add(new Picture(wfs));
        }
    }

    public void Dispose()
    {
        if (_internalPictures == null) return;
        foreach (var picture in _internalPictures)
        {
            picture.Dispose();
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

    public IDictionary<string, string>? Entries {get;}
}


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
        if (string.IsNullOrEmpty(dirUrl) || !Directory.Exists(dirUrl)) return;

        var stem = Path.GetFileNameWithoutExtension(fileUrl);
        var directLrc = Path.Combine(dirUrl, stem + ".lrc");
        if (System.IO.File.Exists(directLrc))
        {
            try
            {
                SyncedLyrics = LrcParser.Parse(System.IO.File.ReadAllText(directLrc));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
        else
        {
            var files = Directory.EnumerateFiles(dirUrl, stem + ".lrc",
                new EnumerationOptions()
                { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

            foreach (var possibleLrcPath in files)
            {
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
        }

        var directTxt = Path.Combine(dirUrl, stem + ".txt");
        if (System.IO.File.Exists(directTxt))
        {
            try
            {
                Lyrics = System.IO.File.ReadAllText(directTxt);
            }
            catch
            {
                // ignored
            }
        }
        else
        {
            var txtFiles = Directory.EnumerateFiles(dirUrl, stem + ".txt",
                new EnumerationOptions()
                { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 2 });

            foreach (var possibleTxtFile in txtFiles)
            {
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
        }

        var coverCandidates = Directory.EnumerateFiles(dirUrl, "*",
            new EnumerationOptions()
            { RecurseSubdirectories = false, IgnoreInaccessible = true });
        foreach (var covr in coverCandidates)
        {
            var fileName = Path.GetFileName(covr);
            if (!fileName.StartsWith("cover", StringComparison.OrdinalIgnoreCase) &&
                !fileName.StartsWith("front", StringComparison.OrdinalIgnoreCase) &&
                !fileName.StartsWith("album", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Debug.WriteLine(covr);
            WrappedFileStream wfs = new(covr);
            if (wfs.MimeType is not SilverMagicBytes.ImageMime)
            {
                wfs.Dispose();
                continue;
            }

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

        _internalPictures = null;
        GC.SuppressFinalize(this);
    }

    public string? Title => null;
    public string? Artist  => null;
    public string? Album  => null;
    public string? Genre => null;
    public int? Year  => null;
    public ulong? Bitrate  => null;
    public ulong? SampleRate  => null;
    public uint? Channels  => null;
    public int? TrackNumber  => null;
    public int? DiscNumber  => null;
    public string[]? Comments => null;
    public double? Duration  => null;
    public string? Lyrics { get; }
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    private List<IPicture>? _internalPictures = [];
    public IReadOnlyList<IPicture>? Pictures => _internalPictures;

    public IDictionary<string, string>? Entries  => null;
}


using System.Text.RegularExpressions;
using ReactiveUI;
using SilverMagicBytes;

//using VideoLibrary;

namespace SilverAudioPlayer.Shared;

public sealed class Song : ReactiveObject, IEquatable<Song>, IEquatable<Guid>, IDisposable
{
    //https://reactgo.com/javascript-check-string-url/
    public readonly Regex URLRegex =
        new(
            "^(https?:\\/\\/)?((([a-z\\d]([a-z\\d-]*[a-z\\d])*)\\.)+[a-z]{2,}|((\\d{1,3}\\.){3}\\d{1,3}))(\\:\\d+)?(\\/[-a-z\\d%_.~+]*)*(\\?[;&a-z\\d%_.~+=-]*)?(\\#[-a-z\\d_]*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ECMAScript);

    private IMetadata? _Metadata;

    public Song(string uri, string name, Guid guid, IMetadata? metadata = null)
    {
        URI = uri.TrimEnd('\0');
        if (URLRegex.IsMatch(uri))
            /*Uri actualurl = new(uri.StartsWith("http")?uri:"https://"+uri);
            if(actualurl.Host== "www.youtube.com" || actualurl.Host == "youtube.com" || actualurl.Host == "youtu.be" || actualurl.Host == "music.youtube.com")
            {
                var y = YouTube.Default.GetAllVideos(uri);
                var z = y.First(x => x.Resolution == -1 && ( x.AudioFormat == AudioFormat.Vorbis));
                Stream = new WrappedYTStream(z);

            }
            else
            {*/
            Stream = new WrappedHttpStream(uri);
        //}
        else
            Stream = new WrappedFileStream(uri);
        Name = name;
        Guid = guid;
        Metadata = metadata;
    }

    public Song(WrappedStream data, string name, Guid guid, IMetadata? metadata = null)
    {
        URI = "";
        Stream = data;
        Name = name;
        Guid = guid;
        Metadata = metadata;
    }

    public WrappedStream Stream { get; set; }
    public string URI { get; set; }
    public string Name { get; set; }
    public Guid Guid { get; set; }

    public IMetadata? Metadata
    {
        get => _Metadata;
        set => this.RaiseAndSetIfChanged(ref _Metadata, value);
    }

    public string TrackNoF => TrackNo();

    public string TitleOrURLF => TitleOrURL();

    public bool Equals(Guid other)
    {
        return Guid.Equals(other);
    }

    public bool Equals(Song? other)
    {
        return other?.Guid == Guid || other?.URI == URI;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Song);
    }

    public override int GetHashCode()
    {
        return Guid.GetHashCode();
    }

    public string TrackNo()
    {
        if (Metadata?.DiscNumber != null) return $"CD{Metadata.DiscNumber}/{Metadata.TrackNumber}";
        return Metadata?.TrackNumber.ToString() ?? "";
    }

    public string TitleOrURL()
    {
        if (string.IsNullOrEmpty(Metadata?.Title)) return URI;
        return Metadata.Title;
    }

    public string ArtistAlbumOptional(bool thingyatstart = false, bool thingyatend = false)
    {
        if (Metadata?.Artist != null && Metadata?.Album != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} - {Metadata.Album} {(thingyatend ? "-" : "")}";
        if (Metadata?.Artist != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} {(thingyatend ? "-" : "")}";
        if (Metadata?.Album != null) return $"{(thingyatstart ? "-" : "")} {Metadata.Album} {(thingyatend ? "-" : "")}";
        return "";
    }

    public override string ToString()
    {
        if (Metadata != null)
            return !string.IsNullOrEmpty(Metadata?.Title)
                ? $"{TrackNo()} {Metadata?.Title} {ArtistAlbumOptional(true)}"
                : Name;
        return Name;
    }

    public void Dispose()
    {
        _Metadata?.Dispose();
        Stream?.Dispose();
    }
}

/*
public class WrappedYTStream : WrappedStream
{

    public WrappedYTStream(YouTubeVideo z)
    {
        this.z = z;
        switch (z.AudioFormat)
        {
            case AudioFormat.Aac:
                m = KnownMimes.AACMime;
                break;
            case AudioFormat.Vorbis:
            case AudioFormat.Opus:
                m = KnownMimes.OGGMime;
                break;
            case AudioFormat.Unknown:
                break;
        }
    }

    MimeType m;
    private YouTubeVideo z;

    public override MimeType MimeType => m;

    public override Stream GetStream()
    {
        return z.Stream();
    }
}*/
public static class MimeTypeExtensions
{
    public static string RealMimeTypeToFakeMimeType(this MimeType realmime)
    {
        return realmime.FileExtensions[0];
    }
}
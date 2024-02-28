using ReactiveUI;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public sealed class Song : ReactiveObject, IEquatable<Song>, IEquatable<Guid>, IDisposable
{
    private IMetadata? _Metadata;
    public Song(WrappedStream data, string name, IMetadata? metadata = null, ILogic? logic=null)
    {
        URI = "";
        if(data is WrappedFileStream w)
        {
            URI=w.URL;
        }
        else if(data is WrappedHttpStream h)
        {
            URI=h.Url;
        }
        Stream = data;
        Name = name;
        Metadata = metadata;
        if (logic is not null)
        {
            Metadata ??= Task.Run(async () => await logic.GetMetadataFromStream(Stream)!).GetAwaiter().GetResult();
        }

        if (metadata is not null)
        {
            var a = metadata?.Title?.GetHashCode()??-1;
            var b = metadata?.Artist?.GetHashCode()??-1;
            var c = metadata?.Album?.GetHashCode()??-1;
            var d = metadata?.Genre?.GetHashCode()??-1;
            List<byte> by =
            [
                .. BitConverter.GetBytes(a),
                .. BitConverter.GetBytes(b),
                .. BitConverter.GetBytes(c),
                .. BitConverter.GetBytes(d),
            ];
            Guid = new Guid(by.ToArray());
        }
        else
        {
            Guid = Guid.NewGuid();
        }
    }
    
    public WrappedStream Stream { get; set; }
    public string URI { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// A Guid assigned to this song by the constructor of the Song object
    /// A collection of the hashes of the metadata Title, Artist, Album and Genre objects (-1 when null) OR a completely random hash
    /// </summary>
    public Guid Guid { get; set; }
    /// <summary>
    /// The song's metadata, it can be also a <see cref="IMetadataCombo"/>
    /// </summary>
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
        return other?.Guid == Guid;
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
        return string.IsNullOrEmpty(Metadata?.Title) ? URI : Metadata.Title;
    }

    public string ArtistAlbumOptional(bool thingyatstart = false, bool thingyatend = false)
    {
        if (Metadata?.Artist != null && Metadata?.Album != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} - {Metadata.Album} {(thingyatend ? "-" : "")}";
        if (Metadata?.Artist != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} {(thingyatend ? "-" : "")}";
        return Metadata?.Album != null ? $"{(thingyatstart ? "-" : "")} {Metadata.Album} {(thingyatend ? "-" : "")}" : "";
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

public static class MimeTypeExtensions
{
    public static string RealMimeTypeToFakeMimeType(this MimeType realmime)
    {
        return realmime.FileExtensions[0];
    }
}
using System.Diagnostics;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public sealed class Song : ReactiveObject, IEquatable<Song>, IEquatable<Guid>, IDisposable
{
    private IMetadata? _Metadata;
    public Song(WrappedStream data, string name, IMetadata? metadata = null, ILogic? logic = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        URI = data switch
        {
            WrappedFileStream w => w.URL,
            WrappedHttpStream h => h.Url,
            _ => null
        };
        Stream = data;
        Name = name;
        Metadata = metadata;
        if (logic is not null)
        {
            try
            {
                var fetchedMetadata = Task.Run(async () => await logic.GetMetadataFromStream(Stream, [metadata])!).GetAwaiter().GetResult();
                if (fetchedMetadata is not null)
                {
                    Metadata = fetchedMetadata;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error loading metadata from stream in Song");
            }
        }
        Guid = GetIdFromMetadata(Metadata, URI);
    }

    public static Guid GetIdFromMetadata(IMetadata? metadata, string? uri = null)
    {
        return CurrentGuidHashingAlgorithm.GetIdFromMetadata(metadata, uri);
    }
    public WrappedStream Stream { get; set; }
    public string? URI { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// A Guid assigned to this song by the constructor of the Song object
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
        _Metadata = null;
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

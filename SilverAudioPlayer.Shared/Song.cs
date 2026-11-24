using System.Diagnostics;
using System.Runtime.CompilerServices;
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
                var metadataTask = logic.GetMetadataFromStream(Stream, metadata is null ? null : [metadata]);
                var fetchedMetadata = metadataTask is not null
                    ? Task.Run(async () => await metadataTask).GetAwaiter().GetResult()
                    : null;
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
        set
        {
            var previous = _Metadata;
            this.RaiseAndSetIfChanged(ref _Metadata, value);
            if (previous is not null && !ReferenceEquals(previous, value) &&
                (value is not IMetadataCombo combo || !combo.OriginalMetadata.Contains(previous)))
            {
                previous.Dispose();
            }

            this.RaisePropertyChanged(nameof(TrackNoF));
            this.RaisePropertyChanged(nameof(TitleOrURLF));
        }
    }

    public string TrackNoF => TrackNo();

    public string TitleOrURLF => TitleOrURL();

    public bool Equals(Guid other)
    {
        return Guid.Equals(other);
    }

    public bool Equals(Song? other)
    {
        return ReferenceEquals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj);
    }

    public override int GetHashCode()
    {
        return RuntimeHelpers.GetHashCode(this);
    }

    public string TrackNo()
    {
        if (Metadata?.DiscNumber != null) return $"CD{Metadata.DiscNumber}/{Metadata.TrackNumber}";
        return Metadata?.TrackNumber.ToString() ?? "";
    }

    public string TitleOrURL()
    {
        if (!string.IsNullOrEmpty(Metadata?.Title)) return Metadata.Title;
        if (!string.IsNullOrEmpty(URI)) return URI;
        return Name ?? "";
    }

    public string ArtistAlbumOptional(bool thingyatstart = false, bool thingyatend = false)
    {
        if (Metadata?.Artist != null && Metadata?.Album != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} - {Metadata.Album} {(thingyatend ? "-" : "")}";
        if (Metadata?.Artist != null)
            return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} {(thingyatend ? "-" : "")}";
        return Metadata?.Album != null
            ? $"{(thingyatstart ? "-" : "")} {Metadata.Album} {(thingyatend ? "-" : "")}"
            : "";
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
        return realmime.FileExtensions is { Length: > 0 } ? realmime.FileExtensions[0] : "";
    }
}
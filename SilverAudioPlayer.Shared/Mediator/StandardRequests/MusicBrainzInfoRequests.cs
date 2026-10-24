namespace SilverAudioPlayer.Shared.StandardRequests;

public record RequestMusicBrainzInfo(Song? Song = null) : IQuery<MusicBrainzInfo?>
{
    public static readonly RequestMusicBrainzInfo Instance = new();
}

public record MusicBrainzInfo
{
    public bool Success { get; init; }
    public Guid? TrackId { get; init; }
    public Guid? RecordingId { get; init; }
    public Guid? AlbumId { get; init; }
    public Guid? ReleaseId { get; init; }
    public Guid? ArtistId { get; init; }
    public Guid? AlbumArtistId { get; init; }
    public Guid? ReleaseTrackId { get; init; }
    public Guid? ReleaseGroupId { get; init; }
    public Guid? WorkId { get; init; }
    public Guid? OriginalAlbumId { get; init; }
    public Guid? OriginalArtistId { get; init; }
    public string? DiscId { get; init; }

    public IReadOnlyList<Guid> ArtistIds { get; init; } = [];
    public IReadOnlyList<Guid> AlbumArtistIds { get; init; } = [];
    public IReadOnlyDictionary<string, string> RawEntries { get; init; } = new Dictionary<string, string>();
}

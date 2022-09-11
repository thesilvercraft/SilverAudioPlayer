namespace SilverAudioPlayer.Shared
{
    public interface IMetadataProvider:ICodeInformation
    {
        public Task<Metadata?> GetMetadata(WrappedStream stream);

        public bool CanGetMetadata(WrappedStream stream);
    }

    public abstract class Metadata
    {
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Album { get; init; }
        public string? Genre { get; init; }
        public int? Year { get; init; }
        public ulong? Bitrate { get; init; }
        public ulong? SampleRate { get; init; }
        public uint? Channels { get; init; }

        public int? TrackNumber { get; init; }
        public int? DiscNumber { get; init; }
        public string[]? Comments { get; init; }

        /// <summary>
        /// duration in milliseconds
        /// </summary>
        public double? Duration { get; init; }

        public string? Lyrics { get; init; }
        public IReadOnlyList<Picture>? Pictures { get; init; }
    }

    public class Picture
    {
        public string? Description { get; init; }
        public string? MimeType { get; init; }
        public byte[]? Data { get; init; }
        public ulong Position { get; init; }
        public PictureType? PicType { get; set; }
        public string? Hash { get; set; }
    }

    public enum PictureType
    {
        //
        // Summary:
        //     Unsupported (i.e. none of the supported values in the enum)
        Unknown = 99,

        //
        // Summary:
        //     Generic
        Generic = 1,

        //
        // Summary:
        //     Front cover
        Front = 2,

        //
        // Summary:
        //     Back cover
        Back = 3,

        //
        // Summary:
        //     CD
        CD = 4,

        //
        // Summary:
        //     File icon
        Icon = 5,

        //
        // Summary:
        //     Leaflet
        Leaflet = 6,

        //
        // Summary:
        //     Lead artist/lead performer/soloist
        LeadArtist = 7,

        //
        // Summary:
        //     Artist/performer
        Artist = 8,

        //
        // Summary:
        //     Conductor
        Conductor = 9,

        //
        // Summary:
        //     Band/Orchestra
        Band = 10,

        //
        // Summary:
        //     Composer
        Composer = 11,

        //
        // Summary:
        //     Lyricist/text writer
        Lyricist = 12,

        //
        // Summary:
        //     Recording location
        RecordingLocation = 13,

        //
        // Summary:
        //     During recording
        DuringRecording = 14,

        //
        // Summary:
        //     During performance
        DuringPerformance = 0xF,

        //
        // Summary:
        //     Movie/video screen capture
        MovieCapture = 0x10,

        //
        // Summary:
        //     Illustration
        Illustration = 18,

        //
        // Summary:
        //     Band/artist logotype
        BandLogo = 19,

        //
        // Summary:
        //     Publisher/Studio logotype
        PublisherLogo = 20
    }
}
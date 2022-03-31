using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Shared
{
    public interface IMetadataProvider
    {
        public Task<Metadata?> GetMetadata(string path);

        public bool CanGetMetadata(string path);
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
        public ulong? FileSize { get; init; }
        public string? FilePath { get; init; }
        public string? FileName { get; init; }
        public string? FileExtension { get; init; }

        public int? TrackNumber { get; init; }
        public string[]? Comments { get; init; }

        /// <summary>
        /// duration in milliseconds
        /// </summary>
        public double? Duration { get; init; }

        public IReadOnlyList<Picture>? Pictures { get; init; }
    }

    public class Picture
    {
        public string? Description { get; init; }
        public string? MimeType { get; init; }
        public byte[]? Data { get; init; }
        public ulong Position { get; init; }
    }
}
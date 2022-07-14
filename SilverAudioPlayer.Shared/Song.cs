using SilverMagicBytes;

namespace SilverAudioPlayer.Shared
{
    public class Song : IEquatable<Song>, IEquatable<Guid>
    {
        public Song(string uri, string name, Guid guid, Metadata? metadata = null)
        {
            URI = uri.TrimEnd('\0');
            bool isfile = !uri.ToLowerInvariant().StartsWith("http");
            if (!isfile)
            {
                Stream = new WrappedHttpStream(uri);
            }
            else
            {
                Stream = new WrappedFileStream(uri);
            }
            //Stream.MimeType = MagicByteCombos.Match(Stream.Stream, 0)?.MimeType ?? (isfile ? new FileInfo(uri).Extension[1..] : ".wav");
            Name = name;
            Guid = guid;
            Metadata = metadata;
        }

        public Song(WrappedStream data, string name, Guid guid, Metadata? metadata = null)
        {
            URI = "";
            Stream = data;
            //Stream.MimeType = data.MimeType.RealMimeTypeToFakeMimeType() ??  ?? ".flac";
            Name = name;
            Guid = guid;
            Metadata = metadata;
        }

        public WrappedStream Stream { get; set; }
        public string URI { get; set; }
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public Metadata? Metadata { get; set; }

        public bool Equals(Song? other)
        {
            return other?.Guid == Guid || other?.URI == URI;
        }

        public bool Equals(Guid other)
        {
            return Guid.Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Song);
        }

        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        private string TrackNo()
        {
            if (Metadata?.DiscNumber != null)
            {
                return $"CD{Metadata.DiscNumber}/{Metadata.TrackNumber}";
            }
            return Metadata?.TrackNumber.ToString() ?? "";
        }

        private string ArtistAlbumOptional(bool thingyatstart = false, bool thingyatend = false)
        {
            if (Metadata?.Artist != null && Metadata?.Album != null)
            {
                return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} - {Metadata.Album} {(thingyatend ? "-" : "")}";
            }
            else if (Metadata?.Artist != null)
            {
                return $"{(thingyatstart ? "-" : "")} {Metadata.Artist} {(thingyatend ? "-" : "")}";
            }
            else if (Metadata?.Album != null)
            {
                return $"{(thingyatstart ? "-" : "")} {Metadata.Album} {(thingyatend ? "-" : "")}";
            }
            return "";
        }

        public string GetTitleOrFileName()
        {
            if (string.IsNullOrEmpty(Metadata?.Title))
            {
                return URI;
            }
            return Metadata.Title;
        }

        public override string ToString()
        {
            if (Metadata != null)
            {
                return !string.IsNullOrEmpty(Metadata?.Title) ? $"{TrackNo()} {Metadata?.Title} {ArtistAlbumOptional(true)}" : Name;
            }
            return Name;
        }
    }
}

public static class MimeTypeExtensions
{
    public static string RealMimeTypeToFakeMimeType(this string realmime)
    {
        return realmime switch
        {
            "audio/flac" => ".flac",
            "audio/mpeg" => ".mp3",
            "audio/aac" => ".aac",
            _ => realmime,
        };
    }
}
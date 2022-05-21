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
            Stream.MimeType = MagicByteCombos.Match(Stream.Stream, 0)?.MimeType ?? (isfile ? new FileInfo(uri).Extension[1..] : ".wav");
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
        string TrackNo()
        {
            if(Metadata?.DiscNumber!=null)
            {
                return $"CD{Metadata.DiscNumber}/{Metadata.TrackNumber}";
               
            }
            return Metadata?.TrackNumber.ToString() ?? "";
        }
        string ArtistAlbumOptional()
        {
            if (Metadata?.Artist != null && Metadata?.Album != null)
            {
                return $"{Metadata.Artist} - {Metadata.Album}";
            }
            else if (Metadata?.Artist != null)
            {
                return Metadata.Artist;
            }
            else if (Metadata?.Album != null)
            {
                return Metadata.Album;
            }
            return "";
        }
        public string GetTitleOrFileName()
        {
            if(string.IsNullOrEmpty(Metadata?.Title))
            {
                return URI;
            }
            return Metadata.Title;
        }
            
        public override string ToString()
        {
            return !string.IsNullOrEmpty(Metadata?.Title)? $"{TrackNo()} {ArtistAlbumOptional()} {Metadata?.Title}" : Name;
        }
    }
}
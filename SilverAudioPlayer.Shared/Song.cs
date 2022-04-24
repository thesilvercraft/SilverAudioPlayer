namespace SilverAudioPlayer.Shared
{
    public class Song : IEquatable<Song>, IEquatable<Guid>
    {
        public Song(string uri, string name, Guid guid, Metadata? metadata = null)
        {
            URI = uri;
            Name = name;
            Guid = guid;
            Metadata = metadata;
        }

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
    }
}
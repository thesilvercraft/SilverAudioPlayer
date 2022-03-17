using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer
{
    public class Song : IEquatable<Song>, IEquatable<Guid>
    {
        public Song(string uri, string name, Guid guid)
        {
            URI = uri;
            Name = name;
            Guid = guid;
        }

        public string URI { get; set; }
        public string Name { get; set; }
        public Guid Guid { get; set; }

        public bool Equals(Song? other)
        {
            return other?.Guid == Guid || other?.URI == URI;
        }

        public bool Equals(Guid other)
        {
            return Guid.Equals(other);
        }
    }
}
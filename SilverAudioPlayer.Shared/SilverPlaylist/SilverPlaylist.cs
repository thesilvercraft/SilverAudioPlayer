using System.IO.Compression;
using System.Xml.Serialization;

namespace SilverAudioPlayer.Shared.SilverPlaylist;

[XmlRoot(ElementName="Album")]
public class Album {
    [XmlAttribute(AttributeName="Name")]
    public string Name { get; set; }
    [XmlAttribute(AttributeName="Year")]
    public string? Year { get; set; }
    [XmlAttribute(AttributeName="MusicBrainz")]
    public string? MusicBrainz { get; set; }
}

[XmlRoot(ElementName="Artist")]
public class Artist {
    [XmlAttribute(AttributeName="Name")]
    public string Name { get; set; }
    [XmlAttribute(AttributeName="MusicBrainz")]
    public string? MusicBrainz { get; set; }
}

[XmlRoot(ElementName="SharedMetadata")]
public class SharedMetadata {
    [XmlElement(ElementName="Album")]
    public List<Album>? Album { get; set; }
    [XmlElement(ElementName="Artist")]
    public List<Artist>? Artist { get; set; }
}
[XmlRoot(ElementName="Source")]
public class Source {
    [XmlAttribute(AttributeName="type")]
    public string Type { get; set; }
    [XmlText]
    public string Text { get; set; }
}

[XmlRoot(ElementName="Sources")]
public class Sources {
    [XmlElement(ElementName="Source")]
    public List<Source> Source { get; set; }
}
[XmlRoot(ElementName="Song")]
public class SerializedSong {
    [XmlElement(ElementName="Sources")]
    public Sources Sources { get; set; }
    [XmlAttribute(AttributeName="Name")]
    public string? Name { get; set; }
    [XmlAttribute(AttributeName="Artist")]
    public string? Artist { get; set; }
    /// <summary>
    /// Album pos can be track no. ex. "13" or disc no. / track no. ex. "1/3" rendered as  CD1 track 3
    /// </summary>
    [XmlAttribute(AttributeName="AlbumPos")]
    public string? AlbumPos { get; set; }
    [XmlAttribute(AttributeName="Album")]
    public string? Album { get; set; }
    [XmlAttribute(AttributeName="MusicBrainz")]
    public string? MusicBrainz { get; set; }
}

[XmlRoot(ElementName="SilverPlayList")]
public class SilverPlayList {
    [XmlElement(ElementName="SharedMetadata")]
    public SharedMetadata? SharedMetadata { get; set; }
    [XmlElement(ElementName="Song")]
    public List<SerializedSong> Song { get; set; }
    [XmlAttribute(AttributeName="Version")]
    public string Version { get; set; }
    [XmlAttribute(AttributeName="Name")]
    public string? Name { get; set; }
    [XmlAttribute(AttributeName="CurrentIndex")]
    public string? CurrentIndex { get; set; }
    [XmlAttribute(AttributeName="CurrentIndexPosition")]
    public string? CurrentIndexPosition { get; set; }
}

public static class SilverPlaylistHelper
{
    public static readonly byte[] MagicBytes = "Spl"u8.ToArray();

    public static void SerialiseBrotli(SilverPlayList playList, Stream streamToWriteTo)
    {
        streamToWriteTo.Write(MagicBytes);
        using BrotliStream stream = new(streamToWriteTo, CompressionLevel.SmallestSize);
        using StreamWriter stringWriter = new(stream);
        XmlSerializer serializer = new(typeof(SilverPlayList));
        serializer.Serialize(stringWriter,playList);
    }
    public static SilverPlayList DeserializeBrotli(Stream streamToReadFrom)
    {
        var check = new byte[3];
        if (streamToReadFrom.Read(check, 0, 3) != 3 || check[0]!= MagicBytes[0] || check[1]!=MagicBytes[1] || check[2]!=MagicBytes[2])
        {
            throw new InvalidOperationException("This is not a valid silver playlist file");
        }
        
        using BrotliStream stream = new(streamToReadFrom, CompressionMode.Decompress);
        XmlSerializer serializer = new(typeof(SilverPlayList));
        return serializer.Deserialize(stream) as SilverPlayList ?? throw new InvalidCastException("The XML decoder returned an object that could not be cast to a SilverPlaylist");
    }

    public const string CurrentPlaylistVersion = "elephant";
    public static SilverPlayList FromQueueCopy(IEnumerable<Song> songs)
    {
        SilverPlayList playList = new()
        {
            Version = CurrentPlaylistVersion,
            Song = new(),
            SharedMetadata = new(),
        };
        foreach (var song in songs)
        {
            var source = new Source() { Type = "url", Text = song.URI };
            switch (song.Stream)
            {
                case WrappedFileStream wrappedFileStream:
                    source.Type = "file";
                    source.Text = wrappedFileStream.URL;
                    break;
                case WrappedHttpStream wrappedHttpStream:
                    source.Type = "http";
                    source.Text = wrappedHttpStream.Url;
                    break;
            }
            var s = new SerializedSong() { Name = song.Metadata?.Title, Album = song.Metadata?.Album, AlbumPos =
                $"{song.Metadata?.DiscNumber ?? 0}/{song.Metadata?.TrackNumber ?? 0}" ,Artist = song.Metadata?.Artist, Sources = new() { Source = new() { source }} };
            playList.Song.Add(s);
        }

        return playList;
    }
}
using System.Composition;
using System.Globalization;
using System.Web;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayListReader.XSPF;

[XmlRoot(ElementName="track")]
public class Track { 

	[XmlElement(ElementName="location", IsNullable = true)] 
	public string Location { get; set; } 

	[XmlElement(ElementName="title", IsNullable = true)] 
	public string Title { get; set; } 

	[XmlElement(ElementName="creator", IsNullable = true)] 
	public string Creator { get; set; } 

	[XmlElement(ElementName="album", IsNullable = true)] 
	public string Album { get; set; } 

	[XmlElement(ElementName="trackNum", IsNullable = true)] 
	public int? TrackNum { get; set; } 

	[XmlElement(ElementName="annotation", IsNullable = true)] 
	public string Annotation { get; set; } 

	[XmlElement(ElementName="image", IsNullable = true)] 
	public string Image { get; set; } 

	[XmlElement(ElementName="duration", IsNullable = true)] 
	public int? Duration { get; set; } 

}

[XmlRoot(ElementName="trackList")]
public class TrackList { 

	[XmlElement(ElementName="track")] 
	public List<Track> Track { get; set; } 
}

[XmlRoot(ElementName="item")]
public class Item { 

	[XmlAttribute(AttributeName="tid")] 
	public int Tid { get; set; } 
}

[XmlType(AnonymousType = true, Namespace = "http://xspf.org/ns/0")]
[XmlRoot(ElementName="playlist",Namespace = "http://xspf.org/ns/0")]

public class Playlist { 

	[XmlElement(ElementName="title", IsNullable = true)] 
	public string Title { get; set; } 

	[XmlElement(ElementName="trackList")] 
	public TrackList TrackList { get; set; } 

	[XmlAttribute(AttributeName="xmlns")] 
	public string Xmlns { get; set; } 

	[XmlAttribute(AttributeName="version")] 
	public int Version { get; set; } 

	[XmlText] 
	public string Text { get; set; } 
}

[Export(typeof(IPlaylistReader))]
public class XSPFPlaylistParser : IPlaylistReader
{
    static MimeType XSPFMime = new PlaylistMime("application/xspf+xml", fileExtensions:[".xspf"]);

    static XSPFPlaylistParser()
    {

        var copy= MagicByteCombos.GetAll().Where(xspf => xspf.MimeType==KnownMimes.XMLMime).ToList();
        foreach (var m in copy)
        {
            MagicByteCombos.RemoveMBC(m);
        }
        MagicByteComboWithMimeType xspf1 = new(XSPFMime, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<playlist "u8.ToArray());
        MagicByteCombos.AddMBC(xspf1);
        MagicByteComboWithMimeType xspf = new(XSPFMime, "<?xml version=\"1.1\" encoding=\"UTF-8\"?>\n<playlist "u8.ToArray());
        MagicByteCombos.AddMBC(xspf);
        foreach (var m in copy)
        {
            MagicByteCombos.AddMBC(m);
        }
    }
    public string Name => "xspf Playlist parser";
    public string Description => "Playlist provider that parses xspf playlists";
    public WrappedStream Icon => null;

    public Version? Version => typeof(XSPFPlaylistParser).Assembly.GetName().Version;
    public string Licenses => "";
    public List<Tuple<Uri, URLType>>? Links => null;
    public IReadOnlyList<MimeType> SupportedMimes => [XSPFMime];

    /// <summary>
    /// Gets the absolute location given a relative name/location, and a parent absolute location
    /// </summary>
    /// <param name="relF">relative file name or location</param>
    /// <param name="parent">parent absolute location</param>
    /// <returns></returns>
    public static string TryGetLocation(string relF, string parent)
    {
        if (relF.StartsWith("http", ignoreCase: true, CultureInfo.InvariantCulture))
        {
            // Making a reasonable assumption that any URL beginning with the HTTP protocol is a full path
            return relF;
        }

        if (parent.StartsWith("http", ignoreCase: true, CultureInfo.InvariantCulture))
        {
            //Making an assumption that the relative file is a subpath of the parent.
            return new Uri(new Uri(parent), relF).ToString();
        }
        else
        {
            if (relF.StartsWith("file://"))
            {
                relF = HttpUtility.UrlDecode( new Uri(relF).AbsolutePath);
            }
            return Path.GetFullPath(Path.Combine(parent, relF));
        }
    }

    public bool TryParse(WrappedStream stream, out IPlaylist? playlist)
    {
        playlist = null;
        if (SupportedMimes.Contains(stream.MimeType))
        {
            string? parentfolder = null;
            if (stream is WrappedFileStream wfs)
            {
                parentfolder = Path.GetDirectoryName(wfs.URL);
            }

            if (stream is WrappedHttpStream whs)
            {
                parentfolder = Path.GetDirectoryName(whs.Url);
            }

            var s = stream.GetStream();
            try
            {
                using var reader = new StreamReader(s);
                XmlSerializer serializer = new XmlSerializer(typeof(Playlist),"http://xspf.org/ns/0");
                
                var xspf = (Playlist)serializer.Deserialize(reader);

                var list = new List<ITrack>();
                foreach (var track in xspf.TrackList.Track)
                {
                    var res = TryGetLocation(track.Location, parentfolder ?? Environment.CurrentDirectory);
                    list.Add(res.StartsWith("http", ignoreCase: true, CultureInfo.InvariantCulture)
                        ? new XSPFTrack(res, "http")
                        : new XSPFTrack(res, "file"));
                    
                }
                playlist = new XSPFPlaylist(list);
                return true;
            }
            finally
            {
                s.Dispose();
            }
        }
        return false;
    }
}
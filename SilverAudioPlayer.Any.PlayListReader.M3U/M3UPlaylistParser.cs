using System.Composition;
using System.Diagnostics;
using System.Globalization;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayListReader.M3U;

[Export(typeof(IPlaylistReader))]
public class M3UPlaylistParser : IPlaylistReader
{
    public string Name => "m3u Playlist parser";
    public string Description => "Playlist provider that parses m3u playlists";
    public WrappedStream Icon => new WrappedEmbeddedResourceStream(typeof(M3UPlaylistParser).Assembly,
        "SilverAudioPlayer.Any.PlayListReader.M3U.icon.svg");

    public Version? Version => typeof(M3UPlaylistParser).Assembly.GetName().Version;
    public string Licenses => "";
    public List<Tuple<Uri, URLType>>? Links => null;
    public IReadOnlyList<MimeType> SupportedMimes => new[] { KnownMimes.M3uMime };
    private const string Header = "#EXTM3U";

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
            /* if (!relF.StartsWith(Path.DirectorySeparatorChar)) //Apparently not needed?
              {
                  relF = Path.DirectorySeparatorChar + relF;
              }*/
            return Path.GetFullPath(Path.Combine(parent, relF));
        }
    }

    public bool TryParse(WrappedStream stream, out IPlaylist? playlist)
    {
        playlist = null;
        //TODO: if url cannot be parsed without telling dotnet that it's a relative url try seeing a file exists at that location
        if (SupportedMimes.Contains((stream.MimeType)))
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
                if (reader.ReadLine() != Header)
                {
                    return false;
                }

                var list = new List<ITrackLoadInfo>();
              //  int length;
              //  bool lengthset = false;
                string? fileTitle = null;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line[0] == '#')
                    {
                        if (line.StartsWith("#EXTINF:"))
                        {
                          //  length = 0;
                            fileTitle = null;
                            string? numeral = null;
                            bool startreadingname = false;
                            for (int i = 7; i < line.Length; i++)
                            {
                                if (numeral == null && line[i] is not (>= '0' and <= '9'))
                                {
                                    numeral = line[7..i];
                                    //lengthset = int.TryParse(numeral, out length);
                                }

                                if (startreadingname || line[i] != ',') continue;
                                startreadingname = true;
                                fileTitle = line[(i + 1)..];
                            }
                        }
                        else if (line.StartsWith("#EXTIMG:"))
                        {
                            reader.ReadLine();
                        }
                        else if (line.StartsWith("#EXTBIN:"))
                        {
                            throw new NotSupportedException("Embedded binary data is not supported, bailing out");
                        }
                    }
                    else
                    {
                        Debug.WriteLine(fileTitle);
                        Debug.WriteLine(line);
                        var res = TryGetLocation(line, parentfolder ?? Environment.CurrentDirectory);
                        Debug.WriteLine(res);
                        list.Add(res.StartsWith("http", ignoreCase: true, CultureInfo.InvariantCulture)
                            ? new M3UTrack(res, "http")
                            : new M3UTrack(res, "file"));
                    }
                }
                playlist = new M3UPlaylist(list);
                return true;
            }
            finally
            {
                if (stream.ShouldDisposeStream)
                {
                    s.Dispose();
                }
            }
        }

        return false;
    }
}
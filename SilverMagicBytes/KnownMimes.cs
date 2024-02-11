namespace SilverMagicBytes;

public static class KnownMimes
{
    public static readonly MimeType MP3Mime = new Mp3Mime();
    public static readonly MimeType FLACMime = new FlacMime();
    public static readonly MimeType WAVMime = new WAVMime();
    public static readonly MimeType AiffMime = new AiffMime();
    public static readonly MimeType MidMime = new MidMime();
    public static readonly MimeType PngMime = new PngMime();
    public static readonly MimeType JPGMime = new JPGMime();
    public static readonly MimeType OGGMime = new OGGMime();
    public static readonly MimeType AACMime = new AACMime();
    public static readonly MimeType OctetMime = new OctetMime();
    public static readonly MimeType SVGMime = new SVGMime();
    public static readonly MimeType Mp4Mime = new Mp4Mime();
    public static readonly MimeType MpegMime = new MpegMime();
    public static readonly MimeType Mp2Mime = MpegMime;
    public static readonly MimeType M3uMime = new M3UMime();
    public static readonly MimeType SplMime = new SPLMime();
    public static readonly MimeType MKVMime =new MKVMime();
    public static readonly MimeType ExtendedTrackerMime =  new ExtendedTrackerMime();
    public static readonly MimeType ModTrackerMime = new ModTrackerMime();
    public static readonly MimeType Mod3TrackerMime = new Mod3TrackerMime();
    public static readonly MimeType S3MTrackerMime = new S3MTrackerMime();
    public static readonly MimeType ImpulseTrackerMime =new ImpulseTrackerMime();
    public static readonly List<MimeType> KnownMimeTypes =
    [
        MidMime,
        MP3Mime,
        FLACMime,
        WAVMime,
        AiffMime,
        MidMime,
        PngMime,
        JPGMime,
        OGGMime,
        AACMime,
        SVGMime,
        Mp4Mime,
        MpegMime,
        Mp2Mime,
        M3uMime,
        SplMime,
        OctetMime,
        MKVMime,
        ExtendedTrackerMime,
        ModTrackerMime,
        Mod3TrackerMime,
        S3MTrackerMime,
        ImpulseTrackerMime
    ];

    public static MimeType? GetKnownMimeByName(string Mime)
    {
        return KnownMimeTypes.Find(x => x.Common == Mime || x.AlternativeTypes.Contains(Mime));
    }

    public static MimeType? GetKnownMimeByExtension(string Extension)
    {
        return KnownMimeTypes.Find(x => x.FileExtensions.Contains(Extension));
    }
}
public class MKVMime : MimeType{
    public MKVMime() : base("video/x-matroska",["audio/x-matroska","video/webm","audio/webm"],[".mkv",".mka",".mks",".mk3d",".webm"])
    {
    }
}
public class M3UMime : PlaylistMime
{
    public M3UMime() : base("application/mpegurl", fileExtensions: [".m3u",".m3u8"], alternativeTypes:["application/x-mpegurl","audio/mpegurl","audio/x-mpegurl","application/vnd.apple.mpegurl", "application/vnd.apple.mpegurl.audio", "application/vnd.apple.mpegurl","audio/mpegurl"])
    {

    }
}
public class SPLMime : PlaylistMime
{
    public SPLMime() : base("application/silverplaylist+br", fileExtensions: [".spl.br"])
    {

    }
}
public class SVGMime : MimeType
{
    public SVGMime() : base("image/svg+xml", fileExtensions: [".svg"])
        {

    }
}

public class Mp3Mime : CompressedAudioMime
{
    public Mp3Mime() : base("audio/mpeg", CompressionType.Lossy, Array.Empty<string>(), [".mp3"])
    {
    }
}
public class Mp4Mime : CompressedVideoMime
{
    public Mp4Mime() : base("video/mp4", CompressionType.Lossy, ["audio/mp4"], [".mp4"])
    {
    }
}
public class MpegMime : CompressedVideoMime
{
    public MpegMime() : base("video/mpeg", CompressionType.Lossy, ["audio/mp2"], [".ts", ".tsv", ".tsa", ".mpg", ".mpeg",".m2p", ".vob"])
    {
    }
}

public class AiffMime : CompressedAudioMime
{
    public AiffMime() : base("audio/aiff", CompressionType.Lossy, ["audio/x-aiff"], [".aiff", ".aif", ".aifc"])
    {
    }
}
public class FlacMime : CompressedAudioMime
{
    public FlacMime() : base("audio/flac", CompressionType.Lossless, [], [".flac"])
    {
    }
}
public class WAVMime : AudioMime
{
    public WAVMime() : base("audio/wave",
        ["audio/vnd.wave", "audio/wav", "audio/x-wav"],
        [".wav"])
    {
    }
}

public class OGGMime : CompressedAudioMime
{
    public OGGMime() : base("audio/vorbis", CompressionType.Lossy, ["audio/x-vorbis+ogg", "audio/ogg", "application/ogg"], [".ogg"])
    {
    }
}

public class AACMime : CompressedAudioMime
{
    public AACMime() : base("audio/aac", CompressionType.Lossy, [], [".aac"])
    {
    }
}


public class MidMime : AudioMime
{
    public MidMime() : base("audio/midi", ["audio/x-midi"], [".mid", ".midi"])
    {
    }
}

public class PngMime : CompressedImageMime
{
    public PngMime() : base("image/png", CompressionType.Lossless, [], [".png"])
    {
    }
}

public class JPGMime : CompressedImageMime
{
    public JPGMime() : base("image/jpeg", CompressionType.Lossy, [], [".jpg", ".jpeg"])
    {
    }
}
public class ImpulseTrackerMime : TrackedAudioMime
{
    public ImpulseTrackerMime() : base("audio/x-it", [], [".it"])
    {
    }
}
public class Mod3TrackerMime : TrackedAudioMime
{
    public Mod3TrackerMime() : base("audio/mo3", [], [".mo3"])
    {
    }
}
public class S3MTrackerMime : TrackedAudioMime
{
    public S3MTrackerMime() : base("audio/s3m", [], [".s3m"])
    {
    }
}
public class ModTrackerMime : TrackedAudioMime
{
    public ModTrackerMime() : base("audio/mod", ["audio/x-mod"], [".mod"])
    {
    }
}
public class ExtendedTrackerMime : TrackedAudioMime
{
    public ExtendedTrackerMime() : base("audio/xm", [], [".xm"])
    {
    }
}
public class TrackedAudioMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : AudioMime(common,
    alternativeTypes, fileExtensions)
{
}
public class AudioMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : MimeType(common,
    alternativeTypes, fileExtensions)
{
}

public class PlaylistMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : MimeType(common,
    alternativeTypes, fileExtensions)
{
}

public class VideoMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : MimeType(common,
    alternativeTypes, fileExtensions)
{
}
public class CompressedVideoMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
    string[]? fileExtensions = null) : VideoMime(common, alternativeTypes, fileExtensions), ICompression
{
    public CompressionType CompressionType { get; set; } = CompressionType;
}
public class ImageMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : MimeType(common,
    alternativeTypes, fileExtensions)
{
}

public class OctetMime : MimeType
{
    public OctetMime() : base("application/octet-stream", ["application/binary"], [".bin"])
    {
    }
}

public class CompressedImageMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
    string[]? fileExtensions = null) : ImageMime(common, alternativeTypes, fileExtensions), ICompression
{
    public CompressionType CompressionType { get; set; } = CompressionType;
}

public class CompressedAudioMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
    string[]? fileExtensions = null) : AudioMime(common, alternativeTypes, fileExtensions), ICompression
{
    public CompressionType CompressionType { get; set; } = CompressionType;
}

public interface ICompression
{
    CompressionType CompressionType { get; }
}

public enum CompressionType
{
    Lossless,
    Lossy
}
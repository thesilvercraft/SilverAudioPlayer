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
    public static readonly MimeType Mp2Mime = new Mp2Mime();

    public static readonly List<MimeType> KnownMimeTypes = new()
    {
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
        OctetMime,
    };

    public static MimeType? GetKnownMimeByName(string Mime)
    {
        return KnownMimeTypes.Find(x => x.Common == Mime || x.AlternativeTypes.Contains(Mime));
    }

    public static MimeType? GetKnownMimeByExtension(string Extension)
    {
        return KnownMimeTypes.Find(x => x.FileExtensions.Contains(Extension));
    }
}

public class SVGMime : MimeType
{
    public SVGMime() : base("image/svg+xml", fileExtensions: new[] {".svg"})
        {

    }
}

public class Mp3Mime : CompressedAudioMime
{
    public Mp3Mime() : base("audio/mpeg", CompressionType.Lossy, Array.Empty<string>(), new[] { ".mp3" })
    {
    }
}
public class Mp4Mime : CompressedVideoMime
{
    public Mp4Mime() : base("video/mp4", CompressionType.Lossy, new[] { "audio/mp4" }, new[] { ".mp4" })
    {
    }
}
public class MpegMime : CompressedVideoMime
{
    public MpegMime() : base("video/mpeg", CompressionType.Lossy, new[] { "audio/mp2;" }, new[] { ".ts", ".tsv", ".tsa", ".mpg", ".mpeg" })
    {
    }
}
public class Mp2Mime : CompressedVideoMime
{
    public Mp2Mime() : base("video/mpeg", CompressionType.Lossy, Array.Empty<string>(), new[] { ".m2p", ".vob", ".mpg", ".mpeg" })
    {
    }
}
public class AiffMime : CompressedAudioMime
{
    public AiffMime() : base("audio/aiff", CompressionType.Lossy, new[] { "audio/x-aiff" }, new[] { ".aiff", ".aif", ".aifc" })
    {
    }
}
public class FlacMime : CompressedAudioMime
{
    public FlacMime() : base("audio/flac", CompressionType.Lossless, Array.Empty<string>(), new[] { ".flac" })
    {
    }
}
public class WAVMime : AudioMime
{
    public WAVMime() : base("audio/wave",
        new[] { "audio/vnd.wave", "audio/wav", "audio/x-wav" },
        new[] { ".wav" })
    {
    }
}

public class OGGMime : CompressedAudioMime
{
    public OGGMime() : base("audio/vorbis", CompressionType.Lossy, new[] { "audio/x-vorbis+ogg", "audio/ogg", "application/ogg" }, new[] { ".ogg" })
    {
    }
}

public class AACMime : CompressedAudioMime
{
    public AACMime() : base("audio/aac", CompressionType.Lossy, Array.Empty<string>(), new[] { ".aac" })
    {
    }
}



public class MidMime : AudioMime
{
    public MidMime() : base("audio/midi", new[] { "audio/x-midi" }, new[] { ".mid", ".midi" })
    {
    }
}

public class PngMime : CompressedImageMime
{
    public PngMime() : base("image/png", CompressionType.Lossless, Array.Empty<string>(), new[] { ".png" })
    {
    }
}

public class JPGMime : CompressedImageMime
{
    public JPGMime() : base("image/jpeg", CompressionType.Lossy, Array.Empty<string>(), new[] { ".jpg", ".jpeg" })
    {
    }
}

public class AudioMime : MimeType
{
    public AudioMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : base(common,
        alternativeTypes, fileExtensions)
    {
    }
}

public class VideoMime : MimeType
{
    public VideoMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : base(common,
        alternativeTypes, fileExtensions)
    {
    }
}
public class CompressedVideoMime : VideoMime, ICompression
{
    public CompressedVideoMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
        string[]? fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
    {
        this.CompressionType = CompressionType;
    }

    public CompressionType CompressionType { get; set; }
}
public class ImageMime : MimeType
{
    public ImageMime(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null) : base(common,
        alternativeTypes, fileExtensions)
    {
    }
}

public class OctetMime : MimeType
{
    public OctetMime() : base("application/octet-stream", new[] { "application/binary" }, new []{".bin"})
    {
    }
}

public class CompressedImageMime : ImageMime, ICompression
{
    public CompressedImageMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
        string[]? fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
    {
        this.CompressionType = CompressionType;
    }

    public CompressionType CompressionType { get; set; }
}

public class CompressedAudioMime : AudioMime, ICompression
{
    public CompressedAudioMime(string common, CompressionType CompressionType, string[]? alternativeTypes = null,
        string[]? fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
    {
        this.CompressionType = CompressionType;
    }

    public CompressionType CompressionType { get; set; }
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
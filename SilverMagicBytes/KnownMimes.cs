namespace SilverMagicBytes
{
    public static class KnownMimes
    {
        public static readonly MimeType MP3Mime = new Mp3Mime();
        public static readonly MimeType FLACMime = new FlacMime();
        public static readonly MimeType WAVMime = new("audio/wave",
            new[] { "audio/vnd.wave", "audio/wav", "audio/x-wav" },
            new[] { ".wav" });
        public static readonly MimeType AiffMime = new("audio/aiff",
          new[] { "audio/x-aiff" },
          new[] { ".aiff", ".aif", ".aifc" });
        public static readonly MimeType MidMime = new WAVMime();
        public static readonly MimeType PngMime = new PngMime();
        public static readonly MimeType JPGMime = new JPGMime();
        public static readonly MimeType OGGMime = new OGGMime();
        public static readonly MimeType AACMime = new AACMime();
        public static readonly MimeType OctetMime = new OctetMime();

        public static readonly List<MimeType> KnownMimeTypes = new()
        {
            MP3Mime,
            FLACMime,
            WAVMime,
            AiffMime,
            MidMime,
            MidMime,
            PngMime,
            JPGMime,
            OGGMime,
            AACMime,
            OctetMime
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
    public class Mp3Mime : CompressedAudioMime
    {
        public Mp3Mime() : base("audio/mpeg", CompressionType.Lossy, Array.Empty<string>(), new[] { ".mp3" })
        {
        }
    }
    public class FlacMime : CompressedAudioMime
    {
        public FlacMime() : base("audio/flac", CompressionType.Lossless, Array.Empty<string>(), new[] { ".flac" })
        {
        }
    }
    public class OGGMime : CompressedAudioMime
    {
        public OGGMime() : base("audio/vorbis", CompressionType.Lossy, Array.Empty<string>(), new[] { ".ogg" })
        {
        }
    }
    public class AACMime : CompressedAudioMime
    {
        public AACMime() : base("audio/aac", CompressionType.Lossy, Array.Empty<string>(), new[] { ".aac" })
        {
        }
    }
    public class WAVMime : AudioMime
    {
        public WAVMime() : base("audio/wave", new[] { "audio/vnd.wave", "audio/wav", "audio/x-wav" }, new[] { ".wav" })
        {
        }
    }

    public class PngMime : CompressedImageMime
    {
        public PngMime() : base("image/png", CompressionType.Lossless, Array.Empty<string>(), new[] { ".png", })
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
        public AudioMime(string common, string[] alternativeTypes = null, string[] fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
        {
        }
    }
    public class ImageMime : MimeType
    {
        public ImageMime(string common, string[] alternativeTypes = null, string[] fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
        {
        }
    }
    public class OctetMime : MimeType
    {
        public OctetMime() : base("application/octet-stream", new[] {"application/binary"}, Array.Empty<string>())
        {
        }
    }
    public class CompressedImageMime : MimeType, ICompression
    {
        public CompressedImageMime(string common, CompressionType CompressionType, string[] alternativeTypes = null, string[] fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
        {
            this.CompressionType = CompressionType;
        }
        public CompressionType CompressionType
        {
            get; set;
        }
    }
    public class CompressedAudioMime : AudioMime, ICompression
    {
        public CompressedAudioMime(string common, CompressionType CompressionType, string[] alternativeTypes = null, string[] fileExtensions = null) : base(common, alternativeTypes, fileExtensions)
        {
            this.CompressionType = CompressionType;
        }

        public CompressionType CompressionType
        {
            get; set;
        }
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
}
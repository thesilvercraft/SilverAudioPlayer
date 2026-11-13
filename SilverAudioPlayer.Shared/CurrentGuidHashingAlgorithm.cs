using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Shared;

public static partial class CurrentGuidHashingAlgorithm
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex ExtraSpacesRegex();

    public static Guid GetIdFromMetadata(IMetadata? metadata, string? filePath)
    {
        var title = CleanString(metadata?.Title);
        var artist = CleanString(metadata?.Artist);
        var album = CleanString(metadata?.Album);

        // 1. If metadata is missing or sparse, fall back to path hashing.
        if (string.IsNullOrWhiteSpace(title) || (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(album)))
        {
            var normalizedPath = NormalizeFilePath(filePath);
            return GetIdFromString($"filepath:{normalizedPath}");
        }

        // 2. Multi-disc / track collision prevention using InvariantCulture.
        var trackNum = metadata?.TrackNumber ?? 0;
        var discNum = metadata?.DiscNumber ?? 1;

        var raw = string.Create(CultureInfo.InvariantCulture, $"{artist}\0{album}\0{discNum}\0{trackNum}\0{title}");
        return GetIdFromString(raw);
    }

    public static Guid GetIdFromString(string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        int maxByteCount = Encoding.UTF8.GetMaxByteCount(str.Length);
        byte[]? rented = null;
        Span<byte> utf8Bytes = maxByteCount <= 512
            ? stackalloc byte[maxByteCount]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int actualBytes = Encoding.UTF8.GetBytes(str, utf8Bytes);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(utf8Bytes[..actualBytes], hash);

            // Standard RFC 9562 Version 8 UUID mutation
            hash[6] = (byte)((hash[6] & 0x0F) | 0x80); // Version 8 (0b1000xxxx)
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant 1 (0b10xxxxxx)

            // Use bigEndian: true so hash[6] maps to the UUID version nibble
            return new Guid(hash[..16], bigEndian: true);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static string CleanString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var normalized = input.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
        return ExtraSpacesRegex().Replace(normalized, " ");
    }

    private static string NormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unknown_file";

        var trimmed = path.Trim();

        // Only treat as URI if it explicitly contains a URI scheme (e.g., "http://", "file://")
        // Avoids truncating local Windows paths containing '#' or '?'
        if (trimmed.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                // Unescape local file URI paths so '#' isn't lost if percent-encoded
                return StripRoot( uri.LocalPath.Replace('\\', '/').Trim().ToLowerInvariant() );
            }
            return uri.GetLeftPart(UriPartial.Path).ToLowerInvariant();
        }

        var normalized = trimmed.Replace('\\', '/').ToLowerInvariant();
        return StripRoot(normalized);
    }

    private static string StripRoot(string normalizedPath)
    {
        // Strip Windows drive root (e.g., "c:/music/song.mp3" -> "music/song.mp3")
        if (normalizedPath.Length >= 2 && char.IsAsciiLetter(normalizedPath[0]) && normalizedPath[1] == ':')
        {
            normalizedPath = normalizedPath[2..];
        }

        return normalizedPath.TrimStart('/');
    }
}
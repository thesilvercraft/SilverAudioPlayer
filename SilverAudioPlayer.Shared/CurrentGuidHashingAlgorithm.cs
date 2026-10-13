using System;
using System.IO;
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

        // Standardize file paths: normalize separators to '/' and strip absolute roots if possible
        var normalizedPath = NormalizeFilePath(filePath);

        // 1. If metadata is missing or sparse, fall back to relative path hashing.
        if (string.IsNullOrWhiteSpace(title) || (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(album)))
        {
            return GetIdFromString($"filepath:{normalizedPath}");
        }

        // 2. Multi-disc / track collision prevention.
        var trackNum = metadata?.TrackNumber ?? 0;
        var discNum = metadata?.DiscNumber ?? 1;

        var raw = $"{artist}\0{album}\0{discNum}\0{trackNum}\0{title}";
        return GetIdFromString(raw);
    }

    public static Guid GetIdFromString(string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(str), hash);

        // Standard RFC 4122 Version 8 UUID mutation
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80); // Version 8
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant 1

        return new Guid(hash[..16]);
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

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return uri.LocalPath.Replace('\\', '/').Trim().ToLowerInvariant();
            }
            return uri.GetLeftPart(UriPartial.Path).ToLowerInvariant();
        }

        var normalized = trimmed.Replace('\\', '/').ToLowerInvariant();

        var queryIdx = normalized.IndexOfAny(['?', '#']);
        return queryIdx != -1 ? normalized[..queryIdx] : normalized;
    }
}

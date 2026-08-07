using System.Security.Cryptography;
using System.Text;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Shared;

public static class CurrentGuidHashingAlgorithm
{
    public static Guid GetIdFromMetadata(IMetadata metadata)
    {
        var artist = metadata.Artist?.Trim().ToLowerInvariant() ?? "";
        var album  = metadata.Album?.Trim().ToLowerInvariant() ?? "";
        var title  = metadata.Title?.Trim().ToLowerInvariant() ?? "";
        var raw = $"{artist}\0{album}\0{title}";
        return GetIdFromString(raw);
    }

    public static Guid GetIdFromString(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(str), hash);
        // Set Variant bits to RFC 4122/9562 (10xx) -> 0x80
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); 
        // Set Version bits to Version 8 (1000) -> 0x80
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80); 
        return new Guid(hash[..16]);
    }

    public static bool HasUsableMetadata(IMetadata metadata)
    {
        return !string.IsNullOrWhiteSpace(metadata.Title)
               || !string.IsNullOrWhiteSpace(metadata.Artist)
               || !string.IsNullOrWhiteSpace(metadata.Album);
    }
}
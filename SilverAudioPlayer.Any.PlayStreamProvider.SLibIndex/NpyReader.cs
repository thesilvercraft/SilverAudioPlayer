using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

internal static partial class NpyReader
{
    public static float[] ReadFloatArray(ZipArchive archive, string entryName, out int rows, out int cols)
    {
        var entry = archive.GetEntry(entryName)
                    ?? throw new FileNotFoundException($"Missing '{entryName}' in npz archive.");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        ParseHeader(bytes, out var descr, out var shape, out var dataOffset);
        rows = shape.Length >= 1 ? shape[0] : 0;
        cols = shape.Length >= 2 ? shape[1] : 1;
        var count = rows * cols;
        var payload = bytes.AsSpan(dataOffset);

        var result = new float[count];
        if (descr is "<f4" or "f4")
        {
            var src = MemoryMarshal.Cast<byte, float>(payload[..(count * 4)]);
            src.CopyTo(result);
        }
        else if (descr is "<f2" or "f2")
        {
            var src = MemoryMarshal.Cast<byte, Half>(payload[..(count * 2)]);
            for (var i = 0; i < count; i++)
            {
                result[i] = (float)src[i];
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported float npy descr '{descr}' in {entryName}.");
        }

        return result;
    }

    public static string[] ReadStringArray(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
                    ?? throw new FileNotFoundException($"Missing '{entryName}' in npz archive.");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        ParseHeader(bytes, out var descr, out var shape, out var dataOffset);
        var count = shape.Length >= 1 ? shape[0] : 0;

        if (!descr.StartsWith("<U", StringComparison.Ordinal) ||
            !int.TryParse(descr.AsSpan(2), out var charLen))
        {
            throw new NotSupportedException($"Unsupported string npy descr '{descr}' in {entryName}.");
        }

        var strideBytes = charLen * 4;
        var payload = bytes.AsSpan(dataOffset);
        var result = new string[count];

        for (var i = 0; i < count; i++)
        {
            var slice = payload.Slice(i * strideBytes, strideBytes);
            result[i] = Encoding.UTF32.GetString(slice).TrimEnd('\0');
        }

        return result;
    }

    private static void ParseHeader(byte[] bytes, out string descr, out int[] shape, out int dataOffset)
    {
        if (bytes.Length < 10 || bytes[0] != 0x93 ||
            bytes[1] != (byte)'N' || bytes[2] != (byte)'U' ||
            bytes[3] != (byte)'M' || bytes[4] != (byte)'P' || bytes[5] != (byte)'Y')
        {
            throw new InvalidDataException("Invalid NPY magic header.");
        }

        var major = bytes[6];
        int headerLen;
        int headerStart;
        if (major == 1)
        {
            headerLen = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
            headerStart = 10;
        }
        else
        {
            headerLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
            headerStart = 12;
        }

        var header = Encoding.ASCII.GetString(bytes, headerStart, headerLen);
        dataOffset = headerStart + headerLen;

        var descrMatch = DescrRegex().Match(header);
        if (!descrMatch.Success)
            throw new InvalidDataException($"Could not parse NPY descr from header: {header}");
        descr = descrMatch.Groups[1].Value;

        var shapeMatch = ShapeRegex().Match(header);
        if (!shapeMatch.Success)
            throw new InvalidDataException($"Could not parse NPY shape from header: {header}");

        shape = shapeMatch.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
    }

    [GeneratedRegex(@"'descr'\s*:\s*'([^']+)'")]
    private static partial Regex DescrRegex();

    [GeneratedRegex(@"'shape'\s*:\s*\(([^)]*)\)")]
    private static partial Regex ShapeRegex();
}

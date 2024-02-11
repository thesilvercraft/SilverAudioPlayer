using System.IO.Compression;
using System.Xml.Serialization;

namespace SilverMagicBytes;

public static class MagicByteCombosHelper
{
    public static void LoadTo(Stream file, List<MagicByteComboWithMimeType> magicByteCombosWithMime)
    {
        using var fs = new BrotliStream(file, CompressionMode.Decompress);
        XmlSerializer xml = new(typeof(MagicByteComboDoc));
        if (xml.Deserialize(fs) is MagicByteComboDoc doc)
        {
            Dictionary<string, MimeType> Mimes = [];
            MimeType? FindFromCommon(string common)
            {
                if (Mimes.TryGetValue(common, out MimeType? value))
                {
                    return value;
                }
                var r = KnownMimes.KnownMimeTypes.FirstOrDefault(x => x.Common == common);
                if (r == null)
                {
                    Console.Error.WriteLine($"Could not find mime for {common}");
                    return null;
                }
                else
                {
                    return Mimes[common] = r;
                }

            }

            foreach (var magicByteCombo in doc.magicBytes)
            {
                var mime = FindFromCommon(magicByteCombo.CommonType);
                magicByteCombosWithMime.Add(new(mime, magicByteCombo.Pattern));
            }
        }
    }
    public static void SaveFrom(List<MagicByteComboWithMimeType> magicByteCombosWithMime, Stream file)
    {
        using var fs = new BrotliStream(file, CompressionLevel.Optimal);
        MagicByteComboDoc doc = new()
        {
            magicBytes = []
        };
        foreach (var magicByteCombo in magicByteCombosWithMime)
        {
            doc.magicBytes.Add(new(magicByteCombo.MimeType.Common, magicByteCombo.Combo));
        }
        XmlSerializer xml = new(typeof(MagicByteComboDoc));
        xml.Serialize(fs, doc);
    }
}
public class MagicByteComboDoc
{
    public List<XmlMagicByte> magicBytes { get; set; }
}
public class XmlMagicByte
{
    public XmlMagicByte(string type, byte?[] pattern)
    {
        CommonType = type;
        Pattern = pattern;
    }
    public XmlMagicByte()
    {

    }
    public string CommonType { get; set; }
    public byte?[] Pattern { get; set; }
}

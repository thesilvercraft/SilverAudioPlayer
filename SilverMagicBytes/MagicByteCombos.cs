namespace SilverMagicBytes;

public static class MagicByteCombos
{
    private static readonly List<MagicByteComboWithMimeType> ByteCombos = new()
    {
        new MagicByteComboWithMimeType(KnownMimes.MP3Mime, 0x49, 0x44, 0x33),
        new MagicByteComboWithMimeType(KnownMimes.MP3Mime, 0xFF, 0xFB),
        new MagicByteComboWithMimeType(KnownMimes.MP3Mime, 0xFF, 0xF3),
        new MagicByteComboWithMimeType(KnownMimes.MP3Mime, 0xFF, 0xF2),
        
        new MagicByteComboWithMimeType(KnownMimes.Mp4Mime, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D),
        new MagicByteComboWithMimeType(KnownMimes.MpegMime, 0x47),
        new MagicByteComboWithMimeType(KnownMimes.Mp2Mime, 0x00, 0x00, 0x01, 0xBA),

        new MagicByteComboWithMimeType(KnownMimes.FLACMime, 0x66, 0x4C, 0x61, 0x43),
        new MagicByteComboWithMimeType(KnownMimes.MidMime, 0x4D, 0x54, 0x68, 0x64),
        new MagicByteComboWithMimeType(KnownMimes.WAVMime, 0x52, 0x49, 0x46, 0x46, null, null, null, null, 0x57, 0x41,
            0x56, 0x45),
        new MagicByteComboWithMimeType(KnownMimes.AiffMime, 0x46, 0x4F, 0x52, 0x4D, null, null, null, null, 0x41, 0x49,
            0x46, 0x46),
        new MagicByteComboWithMimeType(KnownMimes.PngMime, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
        new MagicByteComboWithMimeType(KnownMimes.JPGMime, 0xFF, 0xD8, 0xFF, 0xDB),
        new MagicByteComboWithMimeType(KnownMimes.JPGMime, 0xFF, 0xD8, 0xFF, 0xE0),
        new MagicByteComboWithMimeType(KnownMimes.JPGMime, 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46,
            0x00, 0x01),
        new MagicByteComboWithMimeType(KnownMimes.JPGMime, 0xFF, 0xD8, 0xFF, 0xEE),
        new MagicByteComboWithMimeType(KnownMimes.JPGMime, 0xFF, 0xD8, 0xFF, 0xE1, null, null, 0x45, 0x78, 0x69, 0x66,
            0x00, 0x00),
        new MagicByteComboWithMimeType(KnownMimes.OGGMime, 0x4f, 0x67, 0x67, 0x53),
        new MagicByteComboWithMimeType(KnownMimes.AACMime, 0xFF, 0xF1),
        new MagicByteComboWithMimeType(KnownMimes.AACMime, 0xFF, 0xF9),

        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0xEF, 0xBB, 0xBF, 0x3c, 0x73, 0x76, 0x67),//BOM utf8
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x3c ,0x73 ,0x76 ,0x67),//NO BOM utf8
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x00 ,0x3c ,0x00 ,0x73 ,0x00 ,0x76 ,0x00 ,0x67),//NO BOM utf16BE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0xfe ,0xff ,0x00 ,0x3c ,0x00 ,0x73 ,0x00 ,0x76 ,0x00 ,0x67),//BOM utf16BE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x3c ,0x00 ,0x73 ,0x00 ,0x76 ,0x00 ,0x67 ,0x00),//NO BOM utf16LE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0xff ,0xfe ,0x3c ,0x00 ,0x73 ,0x00 ,0x76 ,0x00 ,0x67 ,0x00),// BOM utf16LE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x00 ,0x00 ,0x00 ,0x3c ,0x00 ,0x00 ,0x00 ,0x73 ,0x00 ,0x00 ,0x00 ,0x76 ,0x00 ,0x00 ,0x00 ,0x67),//NO BOM utf32BE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x00 ,0x00 ,0xfe ,0xff ,0x00 ,0x00 ,0x00 ,0x3c ,0x00 ,0x00 ,0x00 ,0x73 ,0x00 ,0x00 ,0x00 ,0x76 ,0x00 ,0x00 ,0x00 ,0x67),// BOM utf32BE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0x3c ,0x00 ,0x00 ,0x00 ,0x73 ,0x00 ,0x00 ,0x00 ,0x76 ,0x00 ,0x00 ,0x00 ,0x67 ,0x00 ,0x00 ,0x00),//NO BOM utf32LE
        new MagicByteComboWithMimeType(KnownMimes.SVGMime, 0xff ,0xfe ,0x00 ,0x00 ,0x3c ,0x00 ,0x00 ,0x00 ,0x73 ,0x00 ,0x00 ,0x00 ,0x76 ,0x00 ,0x00 ,0x00 ,0x67 ,0x00 ,0x00 ,0x00),//NO BOM utf32LE


        new MagicByteComboWithMimeType(KnownMimes.OctetMime)

    };

    public static MagicByteComboWithMimeType? Match(Stream s, long offset)
    {
        return ByteCombos.Find(combo => combo.Match(s, offset));
    }

    public static bool MatchesAny(Stream s, long offset, string mimetype)
    {
        return ByteCombos.Any(x =>
            (x.MimeType.Common == mimetype || x.MimeType.AlternativeTypes.Contains(mimetype)) && x.Match(s, offset));
    }

    public static void AddMBC(MagicByteComboWithMimeType mbc)
    {
        var x = ByteCombos.FindIndex(y => y.MimeType == KnownMimes.OctetMime);
        if (x == -1)
            ByteCombos.Add(mbc);
        else
            ByteCombos.Insert(x, mbc);
    }

    public static void OverrideMBC(MagicByteComboWithMimeType mbc)
    {
        foreach (var bc in ByteCombos.Where(x => x.MimeType == mbc.MimeType)) ByteCombos.Remove(bc);
        ByteCombos.Add(mbc);
    }

    public static void RemoveMBC(MagicByteComboWithMimeType mbc)
    {
        ByteCombos.Remove(mbc);
    }

    public static MagicByteComboWithMimeType[] GetAll()
    {
        return ByteCombos.ToArray();
    }
}
namespace SilverMagicBytes
{
    public static class MagicByteCombos
    {
        private static List<MagicByteComboWithMimeType> ByteCombos = new()
        {
            //audio/mpeg
            new(".mp3", 0x49, 0x44, 0x33),
            new(".mp3", 0xFF, 0xFB),
            new(".mp3", 0xFF, 0xF3),
            new(".mp3", 0xFF, 0xF2),
            //audio/flac
            new(".flac", 0x66, 0x4C, 0x61, 0x43),
            //audio/wave
            new(".wav", 0x52, 0x49, 0x46, 0x46, null, null, null, null, 0x57, 0x41, 0x56, 0x45),
            //audio/aiff
            new(".aiff", 0x46, 0x4F, 0x52, 0x4D, null, null, null, null, 0x41, 0x49, 0x46, 0x46),
            //audio/.mid
            new(".mid", 0x4D, 0x54, 0x68, 0x64),
            //image/png
            new(".png", 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
             //image/jpg
            new(".jpg", 0xFF, 0xD8, 0xFF, 0xDB),
            new(".jpg", 0xFF, 0xD8, 0xFF, 0xE0),
            new(".jpg", 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01),
            new(".jpg", 0xFF, 0xD8, 0xFF, 0xEE),
            new(".jpg", 0xFF, 0xD8, 0xFF, 0xE1, null, null, 0x45, 0x78, 0x69, 0x66, 0x00, 0x00),
        };

        public static MagicByteComboWithMimeType? Match(Stream s, long offset)
        {
            return ByteCombos.FirstOrDefault(combo => combo.Match(s, offset));
        }

        public static bool MatchesAny(Stream s, long offset, string mimetype)
        {
            return ByteCombos.Where(x => x.MimeType == mimetype).Any(x => x.Match(s, offset));
        }

        public static void AddMBC(MagicByteComboWithMimeType mbc)
        {
            ByteCombos.Add(mbc);
        }

        public static void OverrideMBC(MagicByteComboWithMimeType mbc)
        {
            foreach (var bc in ByteCombos.Where(x => x.MimeType == mbc.MimeType))
            {
                ByteCombos.Remove(bc);
            }
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
}
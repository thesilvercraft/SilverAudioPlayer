namespace SilverMagicBytes
{
    public class MagicByteComboWithMimeType : MagicByteCombo
    {
        public MimeType MimeType { get; set; }

        public MagicByteComboWithMimeType(MimeType mimetype, params byte?[] combo) : base(combo)
        {
            MimeType = mimetype;
        }
    }
    public class MimeType
    {
        public MimeType(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null)
        {
            Common = common;
            AlternativeTypes = alternativeTypes ?? Array.Empty<string>();
            FileExtensions = fileExtensions ?? Array.Empty<string>();
        }

        public string Common { get; set; }
        public string[] AlternativeTypes { get; set; }
        public string[] FileExtensions { get; set; }
    }

}
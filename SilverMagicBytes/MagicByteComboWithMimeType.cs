namespace SilverMagicBytes
{
    public class MagicByteComboWithMimeType : MagicByteCombo
    {
        public string MimeType { get; set; }

        public MagicByteComboWithMimeType(string mimetype, params byte?[] combo) : base(combo)
        {
            MimeType = mimetype;
        }
    }
}
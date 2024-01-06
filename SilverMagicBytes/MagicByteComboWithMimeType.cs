namespace SilverMagicBytes;

public class MagicByteComboWithMimeType : MagicByteCombo
{
    public MagicByteComboWithMimeType(MimeType mimetype, params byte?[] combo) : base(combo)
    {
        MimeType = mimetype;
    }
    public MagicByteComboWithMimeType(MimeType mimetype, byte[] combo) : base(combo)
    {
        MimeType = mimetype;
    }
    public MimeType MimeType { get; set; }
}

public class MimeType(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null)
{
    public string Common { get; set; } = common;
    public string[] AlternativeTypes { get; set; } = alternativeTypes ?? Array.Empty<string>();
    public string[] FileExtensions { get; set; } = fileExtensions ?? Array.Empty<string>();
}
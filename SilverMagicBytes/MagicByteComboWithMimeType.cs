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

    public static bool operator ==(MagicByteComboWithMimeType? a, MagicByteComboWithMimeType? b) => a is null ? b is null : a.Equals(b);

    public static bool operator !=(MagicByteComboWithMimeType? a, MagicByteComboWithMimeType? b) => !(a == b);
    public override bool Equals(object? obj)
    {
        if (base.Equals(obj))
        {
            return true;
        }
        bool IsComboSame(byte?[] a, byte?[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
        if (obj is MagicByteComboWithMimeType other)
        {
            return other.MimeType == MimeType && IsComboSame(Combo, other.Combo);
        }
        return false;
    }
}

public class MimeType(string common, string[]? alternativeTypes = null, string[]? fileExtensions = null)
{
    public string Common { get; set; } = common;
    public string[] AlternativeTypes { get; set; } = alternativeTypes ?? [];
    public string[] FileExtensions { get; set; } = fileExtensions ?? [];
    public static bool operator ==(MimeType? a, MimeType? b) => a is null ? b is null : a.Equals(b);

    public static bool operator !=(MimeType? a, MimeType? b) => !(a == b);
    public override bool Equals(object? obj)
    {
        if (base.Equals(obj))
        {
            return true;
        }
        if (obj is MimeType other)
        {
            return other.Common == Common;
        }
        return false;
    }
}
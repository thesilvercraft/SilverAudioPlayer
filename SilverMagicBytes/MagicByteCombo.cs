namespace SilverMagicBytes;

public class MagicByteCombo
{
    public readonly byte?[] Combo;

    public MagicByteCombo(params byte?[] combo)
    {
        Combo = combo;
    }
    public MagicByteCombo( byte[] combo)
    {
        Combo = combo.Cast<byte?>().ToArray();
    }
    public bool Match(Stream s, long offset)
    {
        if (s.CanSeek) s.Position = offset;
        foreach (var t in Combo)
            if (t != null)
            {
                if (s.ReadByte() != t.Value) return false;
            }
            else
            {
                s.ReadByte();
            }
        return true;
    }
}
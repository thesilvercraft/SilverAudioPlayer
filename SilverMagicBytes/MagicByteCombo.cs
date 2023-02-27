namespace SilverMagicBytes;

public class MagicByteCombo
{
    private readonly byte?[] Combo;

    public MagicByteCombo(params byte?[] combo)
    {
        Combo = combo;
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
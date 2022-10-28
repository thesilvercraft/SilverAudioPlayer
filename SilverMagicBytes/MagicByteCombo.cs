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
        for (var i = 0; i < Combo.Length; i++)
            if (Combo[i] != null)
            {
                if (s.ReadByte() != Combo[i]!.Value) return false;
            }
            else
            {
                s.ReadByte();
            }

        return true;
    }
}
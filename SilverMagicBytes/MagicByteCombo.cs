using System.Diagnostics;

namespace SilverMagicBytes;

public class MagicByteCombo
{
    public readonly byte?[] Combo;

    public MagicByteCombo(params byte?[] combo)
    {
        Combo = combo;
    }
    public MagicByteCombo(byte[] combo)
    {
        Combo = combo.Cast<byte?>().ToArray();
    }
    public bool Match(Stream s, long offset)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(!s.CanRead)
            throw new NotSupportedException($"The stream {nameof(s)} does not support reading? ");
        if(offset!=0 && !s.CanSeek)
            Debug.WriteLine($"SilverMagicBytes.MagicByteCombo.Match: Cannot seek unseekable stream :(");
        var comboOffset = 0;
        if (s.CanSeek)
        {
            s.Position = offset;
            if (Combo[0] is null && Combo.Length>10 && Combo[10] is null)
            {
                comboOffset = Array.FindIndex(Combo, x => x is not null)-1;
                s.Position += comboOffset;
                Debug.WriteLine($"SilverMagicBytes.MagicByteCombo.Match: Skipping {comboOffset} null checks by seeking");
            }
        }
        for (var o = comboOffset; o < Combo.Length; o++)
        {
            if (Combo[o] is {} b)
            {
                if (s.ReadByte() != b) return false;
            }
            else
            {
                if(s.ReadByte()==-1)
                {
                    return false; //EOF
                }
            }
        }
        return true;
    }
}
namespace sndfile;

public unsafe partial struct SF_DITHER_INFO
{
    public int type;

    public double level;

    [NativeTypeName("const char *")]
    public sbyte* name;
}

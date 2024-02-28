namespace sndfile;

public unsafe partial struct SF_FORMAT_INFO
{
    public int format;

    [NativeTypeName("const char *")]
    public sbyte* name;

    [NativeTypeName("const char *")]
    public sbyte* extension;
}

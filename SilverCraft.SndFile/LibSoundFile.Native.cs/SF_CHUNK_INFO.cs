namespace sndfile;

public unsafe partial struct SF_CHUNK_INFO
{
    [NativeTypeName("char[64]")]
    public fixed sbyte id[64];

    [NativeTypeName("unsigned int")]
    public uint id_size;

    [NativeTypeName("unsigned int")]
    public uint datalen;

    public void* data;
}

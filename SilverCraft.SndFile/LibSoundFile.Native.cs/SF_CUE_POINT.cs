namespace sndfile;

public unsafe partial struct SF_CUE_POINT
{
    [NativeTypeName("int32_t")]
    public int indx;

    [NativeTypeName("uint32_t")]
    public uint position;

    [NativeTypeName("int32_t")]
    public int fcc_chunk;

    [NativeTypeName("int32_t")]
    public int chunk_start;

    [NativeTypeName("int32_t")]
    public int block_start;

    [NativeTypeName("uint32_t")]
    public uint sample_offset;

    [NativeTypeName("char[256]")]
    public fixed sbyte name[256];
}

namespace sndfile;

public partial struct SF_EMBED_FILE_INFO
{
    [NativeTypeName("sf_count_t")]
    public nint offset;

    [NativeTypeName("sf_count_t")]
    public nint length;
}

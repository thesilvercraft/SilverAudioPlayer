namespace sndfile;

public partial struct SF_INFO
{
    [NativeTypeName("sf_count_t")]
    public nint frames;

    public int samplerate;

    public int channels;

    public uint format;

    public int sections;

    public int seekable;
}

public struct sf_private_tag
{
    
}
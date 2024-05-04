namespace sndfile;

public enum Whence : uint
{
    Current= Methods.SF_SEEK_CUR,
    Set=Methods.SF_SEEK_SET,
    End=Methods.SF_SEEK_END
}
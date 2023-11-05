namespace sndfile;

public unsafe partial struct SF_CART_TIMER
{
    [NativeTypeName("char[4]")]
    public fixed sbyte usage[4];

    [NativeTypeName("int32_t")]
    public int value;
}

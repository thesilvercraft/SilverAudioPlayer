namespace PortAudio;

public unsafe partial struct PaHostErrorInfo
{
    public PaHostApiTypeId hostApiType;

    [NativeTypeName("long")]
    public nint errorCode;

    [NativeTypeName("const char *")]
    public sbyte* errorText;
}

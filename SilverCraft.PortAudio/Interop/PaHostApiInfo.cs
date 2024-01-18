namespace PortAudio;

public unsafe partial struct PaHostApiInfo
{
    public int structVersion;

    public PaHostApiTypeId type;

    [NativeTypeName("const char *")]
    public sbyte* name;

    public int deviceCount;

    [NativeTypeName("PaDeviceIndex")]
    public int defaultInputDevice;

    [NativeTypeName("PaDeviceIndex")]
    public int defaultOutputDevice;
}

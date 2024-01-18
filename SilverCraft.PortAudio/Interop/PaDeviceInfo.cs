namespace PortAudio;

public unsafe partial struct PaDeviceInfo
{
    public int structVersion;

    [NativeTypeName("const char *")]
    public sbyte* name;

    [NativeTypeName("PaHostApiIndex")]
    public int hostApi;

    public int maxInputChannels;

    public int maxOutputChannels;

    [NativeTypeName("PaTime")]
    public double defaultLowInputLatency;

    [NativeTypeName("PaTime")]
    public double defaultLowOutputLatency;

    [NativeTypeName("PaTime")]
    public double defaultHighInputLatency;

    [NativeTypeName("PaTime")]
    public double defaultHighOutputLatency;

    public double defaultSampleRate;
}

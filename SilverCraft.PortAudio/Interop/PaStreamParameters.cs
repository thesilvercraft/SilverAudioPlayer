namespace PortAudio;

public unsafe partial struct PaStreamParameters
{
    [NativeTypeName("PaDeviceIndex")]
    public int device;

    public int channelCount;

    [NativeTypeName("PaSampleFormat")]
    public nuint sampleFormat;

    [NativeTypeName("PaTime")]
    public double suggestedLatency;

    public void* hostApiSpecificStreamInfo;
}

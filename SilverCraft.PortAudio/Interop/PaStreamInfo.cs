namespace PortAudio;

public partial struct PaStreamInfo
{
    public int structVersion;

    [NativeTypeName("PaTime")]
    public double inputLatency;

    [NativeTypeName("PaTime")]
    public double outputLatency;

    public double sampleRate;
}

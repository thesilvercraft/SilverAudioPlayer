namespace PortAudio;

public unsafe partial struct PaVersionInfo
{
    public int versionMajor;

    public int versionMinor;

    public int versionSubMinor;

    [NativeTypeName("const char *")]
    public sbyte* versionControlRevision;

    [NativeTypeName("const char *")]
    public sbyte* versionText;
}

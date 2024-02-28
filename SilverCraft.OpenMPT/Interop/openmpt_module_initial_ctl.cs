namespace OpenMPT;

public unsafe partial struct openmpt_module_initial_ctl
{
    [NativeTypeName("const char *")]
    public sbyte* ctl;

    [NativeTypeName("const char *")]
    public sbyte* value;
}

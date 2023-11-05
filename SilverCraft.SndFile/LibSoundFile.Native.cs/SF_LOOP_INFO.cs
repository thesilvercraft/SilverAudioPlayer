namespace sndfile;

public unsafe partial struct SF_LOOP_INFO
{
    public short time_sig_num;

    public short time_sig_den;

    public int loop_mode;

    public int num_beats;

    public float bpm;

    public int root_key;

    [NativeTypeName("int[6]")]
    public fixed int future[6];
}

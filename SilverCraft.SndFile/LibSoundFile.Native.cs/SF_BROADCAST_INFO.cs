namespace sndfile;

public unsafe partial struct SF_BROADCAST_INFO
{
    [NativeTypeName("char[256]")]
    public fixed sbyte description[256];

    [NativeTypeName("char[32]")]
    public fixed sbyte originator[32];

    [NativeTypeName("char[32]")]
    public fixed sbyte originator_reference[32];

    [NativeTypeName("char[10]")]
    public fixed sbyte origination_date[10];

    [NativeTypeName("char[8]")]
    public fixed sbyte origination_time[8];

    [NativeTypeName("uint32_t")]
    public uint time_reference_low;

    [NativeTypeName("uint32_t")]
    public uint time_reference_high;

    public short version;

    [NativeTypeName("char[64]")]
    public fixed sbyte umid[64];

    [NativeTypeName("int16_t")]
    public short loudness_value;

    [NativeTypeName("int16_t")]
    public short loudness_range;

    [NativeTypeName("int16_t")]
    public short max_true_peak_level;

    [NativeTypeName("int16_t")]
    public short max_momentary_loudness;

    [NativeTypeName("int16_t")]
    public short max_shortterm_loudness;

    [NativeTypeName("char[180]")]
    public fixed sbyte reserved[180];

    [NativeTypeName("uint32_t")]
    public uint coding_history_size;

    [NativeTypeName("char[256]")]
    public fixed sbyte coding_history[256];
}

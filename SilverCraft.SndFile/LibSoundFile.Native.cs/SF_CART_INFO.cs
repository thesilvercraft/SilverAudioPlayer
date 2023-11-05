using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace sndfile;

public unsafe partial struct SF_CART_INFO
{
    [NativeTypeName("char[4]")]
    public fixed sbyte version[4];

    [NativeTypeName("char[64]")]
    public fixed sbyte title[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte artist[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte cut_id[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte client_id[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte category[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte classification[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte out_cue[64];

    [NativeTypeName("char[10]")]
    public fixed sbyte start_date[10];

    [NativeTypeName("char[8]")]
    public fixed sbyte start_time[8];

    [NativeTypeName("char[10]")]
    public fixed sbyte end_date[10];

    [NativeTypeName("char[8]")]
    public fixed sbyte end_time[8];

    [NativeTypeName("char[64]")]
    public fixed sbyte producer_app_id[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte producer_app_version[64];

    [NativeTypeName("char[64]")]
    public fixed sbyte user_def[64];

    [NativeTypeName("int32_t")]
    public int level_reference;

    [NativeTypeName("SF_CART_TIMER[8]")]
    public _post_timers_e__FixedBuffer post_timers;

    [NativeTypeName("char[276]")]
    public fixed sbyte reserved[276];

    [NativeTypeName("char[1024]")]
    public fixed sbyte url[1024];

    [NativeTypeName("uint32_t")]
    public uint tag_text_size;

    [NativeTypeName("char[256]")]
    public fixed sbyte tag_text[256];

    public partial struct _post_timers_e__FixedBuffer
    {
        public SF_CART_TIMER e0;
        public SF_CART_TIMER e1;
        public SF_CART_TIMER e2;
        public SF_CART_TIMER e3;
        public SF_CART_TIMER e4;
        public SF_CART_TIMER e5;
        public SF_CART_TIMER e6;
        public SF_CART_TIMER e7;

        [UnscopedRef]
        public ref SF_CART_TIMER this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref AsSpan()[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnscopedRef]
        public Span<SF_CART_TIMER> AsSpan() => MemoryMarshal.CreateSpan(ref e0, 8);
    }
}

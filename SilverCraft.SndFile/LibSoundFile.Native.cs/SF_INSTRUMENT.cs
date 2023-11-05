using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace sndfile;

public partial struct SF_INSTRUMENT
{
    public int gain;

    [NativeTypeName("char")]
    public sbyte basenote;

    [NativeTypeName("char")]
    public sbyte detune;

    [NativeTypeName("char")]
    public sbyte velocity_lo;

    [NativeTypeName("char")]
    public sbyte velocity_hi;

    [NativeTypeName("char")]
    public sbyte key_lo;

    [NativeTypeName("char")]
    public sbyte key_hi;

    public int loop_count;

    [NativeTypeName("struct (anonymous struct at /usr/include/sndfile.h:479:2)[16]")]
    public _loops_e__FixedBuffer loops;

    public partial struct _Anonymous_e__Struct
    {
        public int mode;

        [NativeTypeName("uint32_t")]
        public uint start;

        [NativeTypeName("uint32_t")]
        public uint end;

        [NativeTypeName("uint32_t")]
        public uint count;
    }

    public partial struct _loops_e__FixedBuffer
    {
        public _Anonymous_e__Struct e0;
        public _Anonymous_e__Struct e1;
        public _Anonymous_e__Struct e2;
        public _Anonymous_e__Struct e3;
        public _Anonymous_e__Struct e4;
        public _Anonymous_e__Struct e5;
        public _Anonymous_e__Struct e6;
        public _Anonymous_e__Struct e7;
        public _Anonymous_e__Struct e8;
        public _Anonymous_e__Struct e9;
        public _Anonymous_e__Struct e10;
        public _Anonymous_e__Struct e11;
        public _Anonymous_e__Struct e12;
        public _Anonymous_e__Struct e13;
        public _Anonymous_e__Struct e14;
        public _Anonymous_e__Struct e15;

        [UnscopedRef]
        public ref _Anonymous_e__Struct this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref AsSpan()[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnscopedRef]
        public Span<_Anonymous_e__Struct> AsSpan() => MemoryMarshal.CreateSpan(ref e0, 16);
    }
}

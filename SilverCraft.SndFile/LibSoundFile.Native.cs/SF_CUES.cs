using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace sndfile;

public partial struct SF_CUES
{
    [NativeTypeName("uint32_t")]
    public uint cue_count;

    [NativeTypeName("SF_CUE_POINT[100]")]
    public _cue_points_e__FixedBuffer cue_points;

    public partial struct _cue_points_e__FixedBuffer
    {
        public SF_CUE_POINT e0;
        public SF_CUE_POINT e1;
        public SF_CUE_POINT e2;
        public SF_CUE_POINT e3;
        public SF_CUE_POINT e4;
        public SF_CUE_POINT e5;
        public SF_CUE_POINT e6;
        public SF_CUE_POINT e7;
        public SF_CUE_POINT e8;
        public SF_CUE_POINT e9;
        public SF_CUE_POINT e10;
        public SF_CUE_POINT e11;
        public SF_CUE_POINT e12;
        public SF_CUE_POINT e13;
        public SF_CUE_POINT e14;
        public SF_CUE_POINT e15;
        public SF_CUE_POINT e16;
        public SF_CUE_POINT e17;
        public SF_CUE_POINT e18;
        public SF_CUE_POINT e19;
        public SF_CUE_POINT e20;
        public SF_CUE_POINT e21;
        public SF_CUE_POINT e22;
        public SF_CUE_POINT e23;
        public SF_CUE_POINT e24;
        public SF_CUE_POINT e25;
        public SF_CUE_POINT e26;
        public SF_CUE_POINT e27;
        public SF_CUE_POINT e28;
        public SF_CUE_POINT e29;
        public SF_CUE_POINT e30;
        public SF_CUE_POINT e31;
        public SF_CUE_POINT e32;
        public SF_CUE_POINT e33;
        public SF_CUE_POINT e34;
        public SF_CUE_POINT e35;
        public SF_CUE_POINT e36;
        public SF_CUE_POINT e37;
        public SF_CUE_POINT e38;
        public SF_CUE_POINT e39;
        public SF_CUE_POINT e40;
        public SF_CUE_POINT e41;
        public SF_CUE_POINT e42;
        public SF_CUE_POINT e43;
        public SF_CUE_POINT e44;
        public SF_CUE_POINT e45;
        public SF_CUE_POINT e46;
        public SF_CUE_POINT e47;
        public SF_CUE_POINT e48;
        public SF_CUE_POINT e49;
        public SF_CUE_POINT e50;
        public SF_CUE_POINT e51;
        public SF_CUE_POINT e52;
        public SF_CUE_POINT e53;
        public SF_CUE_POINT e54;
        public SF_CUE_POINT e55;
        public SF_CUE_POINT e56;
        public SF_CUE_POINT e57;
        public SF_CUE_POINT e58;
        public SF_CUE_POINT e59;
        public SF_CUE_POINT e60;
        public SF_CUE_POINT e61;
        public SF_CUE_POINT e62;
        public SF_CUE_POINT e63;
        public SF_CUE_POINT e64;
        public SF_CUE_POINT e65;
        public SF_CUE_POINT e66;
        public SF_CUE_POINT e67;
        public SF_CUE_POINT e68;
        public SF_CUE_POINT e69;
        public SF_CUE_POINT e70;
        public SF_CUE_POINT e71;
        public SF_CUE_POINT e72;
        public SF_CUE_POINT e73;
        public SF_CUE_POINT e74;
        public SF_CUE_POINT e75;
        public SF_CUE_POINT e76;
        public SF_CUE_POINT e77;
        public SF_CUE_POINT e78;
        public SF_CUE_POINT e79;
        public SF_CUE_POINT e80;
        public SF_CUE_POINT e81;
        public SF_CUE_POINT e82;
        public SF_CUE_POINT e83;
        public SF_CUE_POINT e84;
        public SF_CUE_POINT e85;
        public SF_CUE_POINT e86;
        public SF_CUE_POINT e87;
        public SF_CUE_POINT e88;
        public SF_CUE_POINT e89;
        public SF_CUE_POINT e90;
        public SF_CUE_POINT e91;
        public SF_CUE_POINT e92;
        public SF_CUE_POINT e93;
        public SF_CUE_POINT e94;
        public SF_CUE_POINT e95;
        public SF_CUE_POINT e96;
        public SF_CUE_POINT e97;
        public SF_CUE_POINT e98;
        public SF_CUE_POINT e99;

        [UnscopedRef]
        public ref SF_CUE_POINT this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref AsSpan()[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnscopedRef]
        public Span<SF_CUE_POINT> AsSpan() => MemoryMarshal.CreateSpan(ref e0, 100);
    }
}

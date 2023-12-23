using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static System.Int64;

namespace sndfile;
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
public static unsafe partial class Methods
{
    public const string dllName = "libsndfile";

    public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != dllName) return IntPtr.Zero;
        return OperatingSystem.IsWindows() ? NativeLibrary.Load("sndfile.dll", assembly, searchPath) : IntPtr.Zero;
    }

    public static unsafe void sfe_copy_data_int(ref sf_private_tag* outfile, ref sf_private_tag* infile, int channels)
    {
        int[]	data =new int[4096] ;
        int		frames, readcount ;
        frames = 4096 / channels ;
        readcount = frames ;
        fixed (int* dataPtr = data)
        {
            while (readcount > 0)
            {
                readcount = (int)sf_readf_int(infile, dataPtr, frames);
                sf_writef_int(outfile, dataPtr, readcount);
            }
        }
    }

    public static unsafe int sfe_copy_data_fp(ref sf_private_tag* outfile, ref sf_private_tag* infile, int channels, int normalize)
    {
        double[] data = new double[4096];
        double max;
        nint frames, readcount, k;
        frames = 4096 / channels;
        readcount = frames;
        sf_command(infile, SFC_CALC_SIGNAL_MAX, &max, sizeof(double));
        if (!double.IsNormal(max))
        {
            return 1 ;
        }

        fixed (double* dataPtr = data)
        {
            if (normalize == 0 && max < 1.0)
            {
                while (readcount > 0)
                {
                    readcount = sf_readf_double(infile, dataPtr, frames);
                    sf_writef_double(outfile, dataPtr, readcount);
                }
            }
            else
            {
                sf_command(infile, SFC_SET_NORM_DOUBLE, null, 0);
                while (readcount > 0)
                {
                    readcount = sf_readf_double(infile, dataPtr, frames);
                    for (k = 0; k < readcount * channels; k++)
                    {
                        data[k] /= max;

                        if (!double.IsFinite(data[k])) /* infinite or NaN */
                            return 1;
                    }

                    sf_writef_double(outfile, dataPtr, readcount);
                }
            }
        }

        return 0 ;
    }
    public const uint SF_FORMAT_WAV = 0x010000;
    public const uint SF_FORMAT_AIFF = 0x020000;
    public const uint SF_FORMAT_AU = 0x030000;
    public const uint SF_FORMAT_RAW = 0x040000;
    public const uint SF_FORMAT_PAF = 0x050000;
    public const uint SF_FORMAT_SVX = 0x060000;
    public const uint SF_FORMAT_NIST = 0x070000;
    public const uint SF_FORMAT_VOC = 0x080000;
    public const uint SF_FORMAT_IRCAM = 0x0A0000;
    public const uint SF_FORMAT_W64 = 0x0B0000;
    public const uint SF_FORMAT_MAT4 = 0x0C0000;
    public const uint SF_FORMAT_MAT5 = 0x0D0000;
    public const uint SF_FORMAT_PVF = 0x0E0000;
    public const uint SF_FORMAT_XI = 0x0F0000;
    public const uint SF_FORMAT_HTK = 0x100000;
    public const uint SF_FORMAT_SDS = 0x110000;
    public const uint SF_FORMAT_AVR = 0x120000;
    public const uint SF_FORMAT_WAVEX = 0x130000;
    public const uint SF_FORMAT_SD2 = 0x160000;
    public const uint SF_FORMAT_FLAC = 0x170000;
    public const uint SF_FORMAT_CAF = 0x180000;
    public const uint SF_FORMAT_WVE = 0x190000;
    public const uint SF_FORMAT_OGG = 0x200000;
    public const uint SF_FORMAT_MPC2K = 0x210000;
    public const uint SF_FORMAT_RF64 = 0x220000;
    public const uint SF_FORMAT_MPEG = 0x230000;
    public const uint SF_FORMAT_PCM_S8 = 0x0001;
    public const uint SF_FORMAT_PCM_16 = 0x0002;
    public const uint SF_FORMAT_PCM_24 = 0x0003;
    public const uint SF_FORMAT_PCM_32 = 0x0004;
    public const uint SF_FORMAT_PCM_U8 = 0x0005;
    public const uint SF_FORMAT_FLOAT = 0x0006;
    public const uint SF_FORMAT_DOUBLE = 0x0007;
    public const uint SF_FORMAT_ULAW = 0x0010;
    public const uint SF_FORMAT_ALAW = 0x0011;
    public const uint SF_FORMAT_IMA_ADPCM = 0x0012;
    public const uint SF_FORMAT_MS_ADPCM = 0x0013;
    public const uint SF_FORMAT_GSM610 = 0x0020;
    public const uint SF_FORMAT_VOX_ADPCM = 0x0021;
    public const uint SF_FORMAT_NMS_ADPCM_16 = 0x0022;
    public const uint SF_FORMAT_NMS_ADPCM_24 = 0x0023;
    public const uint SF_FORMAT_NMS_ADPCM_32 = 0x0024;
    public const uint SF_FORMAT_G721_32 = 0x0030;
    public const uint SF_FORMAT_G723_24 = 0x0031;
    public const uint SF_FORMAT_G723_40 = 0x0032;
    public const uint SF_FORMAT_DWVW_12 = 0x0040;
    public const uint SF_FORMAT_DWVW_16 = 0x0041;
    public const uint SF_FORMAT_DWVW_24 = 0x0042;
    public const uint SF_FORMAT_DWVW_N = 0x0043;
    public const uint SF_FORMAT_DPCM_8 = 0x0050;
    public const uint SF_FORMAT_DPCM_16 = 0x0051;
    public const uint SF_FORMAT_VORBIS = 0x0060;
    public const uint SF_FORMAT_OPUS = 0x0064;
    public const uint SF_FORMAT_ALAC_16 = 0x0070;
    public const uint SF_FORMAT_ALAC_20 = 0x0071;
    public const uint SF_FORMAT_ALAC_24 = 0x0072;
    public const uint SF_FORMAT_ALAC_32 = 0x0073;
    public const uint SF_FORMAT_MPEG_LAYER_I = 0x0080;
    public const uint SF_FORMAT_MPEG_LAYER_II = 0x0081;
    public const uint SF_FORMAT_MPEG_LAYER_III = 0x0082;
    public const uint SF_ENDIAN_FILE = 0x00000000;
    public const uint SF_ENDIAN_LITTLE = 0x10000000;
    public const uint SF_ENDIAN_BIG = 0x20000000;
    public const uint SF_ENDIAN_CPU = 0x30000000;
    public const uint SF_FORMAT_SUBMASK = 0x0000FFFF;
    public const uint SF_FORMAT_TYPEMASK = 0x0FFF0000;
    public const uint SF_FORMAT_ENDMASK = 0x30000000;

    public const uint SFC_GET_LIB_VERSION = 0x1000;
    public const uint SFC_GET_LOG_INFO = 0x1001;
    public const uint SFC_GET_CURRENT_SF_INFO = 0x1002;
    public const uint SFC_GET_NORM_DOUBLE = 0x1010;
    public const uint SFC_GET_NORM_FLOAT = 0x1011;
    public const uint SFC_SET_NORM_DOUBLE = 0x1012;
    public const uint SFC_SET_NORM_FLOAT = 0x1013;
    public const uint SFC_SET_SCALE_FLOAT_INT_READ = 0x1014;
    public const uint SFC_SET_SCALE_INT_FLOAT_WRITE = 0x1015;
    public const uint SFC_GET_SIMPLE_FORMAT_COUNT = 0x1020;
    public const uint SFC_GET_SIMPLE_FORMAT = 0x1021;
    public const uint SFC_GET_FORMAT_INFO = 0x1028;
    public const uint SFC_GET_FORMAT_MAJOR_COUNT = 0x1030;
    public const uint SFC_GET_FORMAT_MAJOR = 0x1031;
    public const uint SFC_GET_FORMAT_SUBTYPE_COUNT = 0x1032;
    public const uint SFC_GET_FORMAT_SUBTYPE = 0x1033;
    public const uint SFC_CALC_SIGNAL_MAX = 0x1040;
    public const uint SFC_CALC_NORM_SIGNAL_MAX = 0x1041;
    public const uint SFC_CALC_MAX_ALL_CHANNELS = 0x1042;
    public const uint SFC_CALC_NORM_MAX_ALL_CHANNELS = 0x1043;
    public const uint SFC_GET_SIGNAL_MAX = 0x1044;
    public const uint SFC_GET_MAX_ALL_CHANNELS = 0x1045;
    public const uint SFC_SET_ADD_PEAK_CHUNK = 0x1050;
    public const uint SFC_UPDATE_HEADER_NOW = 0x1060;
    public const uint SFC_SET_UPDATE_HEADER_AUTO = 0x1061;
    public const uint SFC_FILE_TRUNCATE = 0x1080;
    public const uint SFC_SET_RAW_START_OFFSET = 0x1090;
    public const uint SFC_SET_DITHER_ON_WRITE = 0x10A0;
    public const uint SFC_SET_DITHER_ON_READ = 0x10A1;
    public const uint SFC_GET_DITHER_INFO_COUNT = 0x10A2;
    public const uint SFC_GET_DITHER_INFO = 0x10A3;
    public const uint SFC_GET_EMBED_FILE_INFO = 0x10B0;
    public const uint SFC_SET_CLIPPING = 0x10C0;
    public const uint SFC_GET_CLIPPING = 0x10C1;
    public const uint SFC_GET_CUE_COUNT = 0x10CD;
    public const uint SFC_GET_CUE = 0x10CE;
    public const uint SFC_SET_CUE = 0x10CF;
    public const uint SFC_GET_INSTRUMENT = 0x10D0;
    public const uint SFC_SET_INSTRUMENT = 0x10D1;
    public const uint SFC_GET_LOOP_INFO = 0x10E0;
    public const uint SFC_GET_BROADCAST_INFO = 0x10F0;
    public const uint SFC_SET_BROADCAST_INFO = 0x10F1;
    public const uint SFC_GET_CHANNEL_MAP_INFO = 0x1100;
    public const uint SFC_SET_CHANNEL_MAP_INFO = 0x1101;
    public const uint SFC_RAW_DATA_NEEDS_ENDSWAP = 0x1110;
    public const uint SFC_WAVEX_SET_AMBISONIC = 0x1200;
    public const uint SFC_WAVEX_GET_AMBISONIC = 0x1201;
    public const uint SFC_RF64_AUTO_DOWNGRADE = 0x1210;
    public const uint SFC_SET_VBR_ENCODING_QUALITY = 0x1300;
    public const uint SFC_SET_COMPRESSION_LEVEL = 0x1301;
    public const uint SFC_SET_OGG_PAGE_LATENCY_MS = 0x1302;
    public const uint SFC_SET_OGG_PAGE_LATENCY = 0x1303;
    public const uint SFC_GET_OGG_STREAM_SERIALNO = 0x1306;
    public const uint SFC_GET_BITRATE_MODE = 0x1304;
    public const uint SFC_SET_BITRATE_MODE = 0x1305;
    public const uint SFC_SET_CART_INFO = 0x1400;
    public const uint SFC_GET_CART_INFO = 0x1401;
    public const uint SFC_SET_ORIGINAL_SAMPLERATE = 0x1500;
    public const uint SFC_GET_ORIGINAL_SAMPLERATE = 0x1501;
    public const uint SFC_TEST_IEEE_FLOAT_REPLACE = 0x6001;
    public const uint SFC_SET_ADD_HEADER_PAD_CHUNK = 0x1051;
    public const uint SFC_SET_ADD_DITHER_ON_WRITE = 0x1070;
    public const uint SFC_SET_ADD_DITHER_ON_READ = 0x1071;

    public const uint SF_STR_TITLE = 0x01;
    public const uint SF_STR_COPYRIGHT = 0x02;
    public const uint SF_STR_SOFTWARE = 0x03;
    public const uint SF_STR_ARTIST = 0x04;
    public const uint SF_STR_COMMENT = 0x05;
    public const uint SF_STR_DATE = 0x06;
    public const uint SF_STR_ALBUM = 0x07;
    public const uint SF_STR_LICENSE = 0x08;
    public const uint SF_STR_TRACKNUMBER = 0x09;
    public const uint SF_STR_GENRE = 0x10;

    public const uint SF_FALSE = 0;
    public const uint SF_TRUE = 1;

    public const uint SF_AMBISONIC_NONE = 0x40;
    public const uint SF_AMBISONIC_B_FORMAT = 0x41;

    public const uint SF_ERR_NO_ERROR = 0;
    public const uint SF_ERR_UNRECOGNISED_FORMAT = 1;
    public const uint SF_ERR_SYSTEM = 2;
    public const uint SF_ERR_MALFORMED_FILE = 3;
    public const uint SF_ERR_UNSUPPORTED_ENCODING = 4;

    public const uint SF_CHANNEL_MAP_INVALID = 0;
    public const uint SF_CHANNEL_MAP_MONO = 1;
    public const uint SF_CHANNEL_MAP_LEFT = 2;
    public const uint SF_CHANNEL_MAP_RIGHT = 3;
    public const uint SF_CHANNEL_MAP_CENTER = 4;
    public const uint SF_CHANNEL_MAP_FRONT_LEFT = 5;
    public const uint SF_CHANNEL_MAP_FRONT_RIGHT = 6;
    public const uint SF_CHANNEL_MAP_FRONT_CENTER = 7;
    public const uint SF_CHANNEL_MAP_REAR_CENTER = 8;
    public const uint SF_CHANNEL_MAP_REAR_LEFT = 9;
    public const uint SF_CHANNEL_MAP_REAR_RIGHT = 10;
    public const uint SF_CHANNEL_MAP_LFE = 11;
    public const uint SF_CHANNEL_MAP_FRONT_LEFT_OF_CENTER = 12;
    public const uint SF_CHANNEL_MAP_FRONT_RIGHT_OF_CENTER = 13;
    public const uint SF_CHANNEL_MAP_SIDE_LEFT = 14;
    public const uint SF_CHANNEL_MAP_SIDE_RIGHT = 15;
    public const uint SF_CHANNEL_MAP_TOP_CENTER = 16;
    public const uint SF_CHANNEL_MAP_TOP_FRONT_LEFT = 17;
    public const uint SF_CHANNEL_MAP_TOP_FRONT_RIGHT = 18;
    public const uint SF_CHANNEL_MAP_TOP_FRONT_CENTER = 19;
    public const uint SF_CHANNEL_MAP_TOP_REAR_LEFT = 20;
    public const uint SF_CHANNEL_MAP_TOP_REAR_RIGHT = 21;
    public const uint SF_CHANNEL_MAP_TOP_REAR_CENTER = 22;
    public const uint SF_CHANNEL_MAP_AMBISONIC_B_W = 23;
    public const uint SF_CHANNEL_MAP_AMBISONIC_B_X = 24;
    public const uint SF_CHANNEL_MAP_AMBISONIC_B_Y = 25;
    public const uint SF_CHANNEL_MAP_AMBISONIC_B_Z = 26;
    public const uint SF_CHANNEL_MAP_MAX = 27;

    public const uint SF_BITRATE_MODE_CONSTANT = 0;
    public const uint SF_BITRATE_MODE_AVERAGE = 1;
    public const uint SF_BITRATE_MODE_VARIABLE = 2;

    public const uint SFD_DEFAULT_LEVEL = 0;
    public const uint SFD_CUSTOM_LEVEL = 0x40000000;
    public const uint SFD_NO_DITHER = 500;
    public const uint SFD_WHITE = 501;
    public const uint SFD_TRIANGULAR_PDF = 502;

    public const uint SF_LOOP_NONE = 800;
    public const uint SF_LOOP_FORWARD = 801;
    public const uint SF_LOOP_BACKWARD = 802;
    public const uint SF_LOOP_ALTERNATING = 803;
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SNDFILE *")]
    public static extern sf_private_tag* sf_open([NativeTypeName("const char *")] string path, Mode mode, SF_INFO* sfinfo);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("SNDFILE *")]
    public static extern sf_private_tag* sf_open_fd(int fd, Mode mode, SF_INFO* sfinfo, int close_desc);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe sf_private_tag* sf_open_virtual(
        ref SF_VIRTUAL_IO @virtual,
        Mode mode,
        ref SF_INFO info,
        IntPtr userData
    );

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_error([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern IntPtr sf_strerror([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* sf_error_number(int errnum);
    [Obsolete("Use sf_error_number")]
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_perror([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);
    [Obsolete("Use sf_strerror")]
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_error_str([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("char *")] sbyte* str, [NativeTypeName("size_t")] nuint len);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_command([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, uint command, void* data, int datasize);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_format_check([NativeTypeName("const SF_INFO *")] SF_INFO* info);

    public const uint SF_SEEK_SET = 0;
    public const uint SF_SEEK_CUR = 1;
    public const uint SF_SEEK_END = 2;

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_seek([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("sf_count_t")] nint frames, Whence whence);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_set_string([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, int str_type, [NativeTypeName("const char *")] char* str);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* sf_get_string([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, int str_type);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* sf_version_string();

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_current_byterate([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_read_raw([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, void* ptr, [NativeTypeName("sf_count_t")] nint bytes);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_write_raw([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const void *")] void* ptr, [NativeTypeName("sf_count_t")] nint bytes);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_readf_short([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, short* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_writef_short([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const short *")] short* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_readf_int([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, int* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_writef_int([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const int *")] int* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_readf_float([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, float* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_writef_float([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const float *")] float* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_readf_double([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, double* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_writef_double([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const double *")] double* ptr, [NativeTypeName("sf_count_t")] nint frames);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_read_short([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, short* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_write_short([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const short *")] short* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_read_int([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, int* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_write_int([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const int *")] int* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_read_float([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, float* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_write_float([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const float *")] float* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_read_double([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, double* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("sf_count_t")]
    public static extern nint sf_write_double([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const double *")] double* ptr, [NativeTypeName("sf_count_t")] nint items);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_close([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void sf_write_sync([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_set_chunk([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const SF_CHUNK_INFO *")] SF_CHUNK_INFO* chunk_info);

   /* [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SF_CHUNK_ITERATOR* sf_get_chunk_iterator([NativeTypeName("SNDFILE *")] sf_private_tag* sndfile, [NativeTypeName("const SF_CHUNK_INFO *")] SF_CHUNK_INFO* chunk_info);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern SF_CHUNK_ITERATOR* sf_next_chunk_iterator(SF_CHUNK_ITERATOR* iterator);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_get_chunk_size([NativeTypeName("const SF_CHUNK_ITERATOR *")] SF_CHUNK_ITERATOR* it, SF_CHUNK_INFO* chunk_info);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sf_get_chunk_data([NativeTypeName("const SF_CHUNK_ITERATOR *")] SF_CHUNK_ITERATOR* it, SF_CHUNK_INFO* chunk_info);*/

    [NativeTypeName("#define SF_STR_FIRST SF_STR_TITLE")]
    public const uint SF_STR_FIRST = SF_STR_TITLE;

    [NativeTypeName("#define SF_STR_LAST SF_STR_GENRE")]
    public const uint SF_STR_LAST = SF_STR_GENRE;

    [NativeTypeName("#define SF_COUNT_MAX INT64_MAX")]
    public const long SF_COUNT_MAX = MaxValue; //9223372036854775807
}
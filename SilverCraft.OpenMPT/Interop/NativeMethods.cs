using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenMPT;
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
public struct openmpt_module
{

}
public static unsafe partial class NativeMethods
{
    public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName) return IntPtr.Zero;
        return OperatingSystem.IsWindows() ? NativeLibrary.Load("libopenmpt.dll", assembly, searchPath) : IntPtr.Zero;
    }
    public const string LibraryName = "libopenmpt.so";
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint openmpt_get_library_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint openmpt_get_core_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_free_string([NativeTypeName("const char *")] sbyte* str);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_get_string([NativeTypeName("const char *")] sbyte* key);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_get_supported_extensions();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_is_extension_supported([NativeTypeName("const char *")] sbyte* extension);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_log_func_default([NativeTypeName("const char *")] sbyte* message, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_log_func_silent([NativeTypeName("const char *")] sbyte* message, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_is_transient(int error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_error_string(int error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_func_default(int error, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_func_log(int error, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_func_store(int error, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_func_ignore(int error, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_error_func_errno(int error, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void* openmpt_error_func_errno_userdata(int* error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern double openmpt_could_open_probability(openmpt_stream_callbacks stream_callbacks, void* stream, double effort, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern double openmpt_could_open_propability(openmpt_stream_callbacks stream_callbacks, void* stream, double effort, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* user);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_could_open_probability2(openmpt_stream_callbacks stream_callbacks, void* stream, double effort, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_probe_file_header_get_recommended_size();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_probe_file_header([NativeTypeName("uint64_t")] nuint flags, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size, [NativeTypeName("uint64_t")] nuint filesize, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_probe_file_header_without_filesize([NativeTypeName("uint64_t")] nuint flags, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_probe_file_header_from_stream([NativeTypeName("uint64_t")] nuint flags, openmpt_stream_callbacks stream_callbacks, void* stream, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern openmpt_module* openmpt_module_create(openmpt_stream_callbacks stream_callbacks, void* stream, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("const openmpt_module_initial_ctl *")] openmpt_module_initial_ctl* ctls);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern openmpt_module* openmpt_module_create2(openmpt_stream_callbacks stream_callbacks, IntPtr stream, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message, [NativeTypeName("const openmpt_module_initial_ctl *")] openmpt_module_initial_ctl* ctls);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern openmpt_module* openmpt_module_create_from_memory([NativeTypeName("const void *")] void* filedata, [NativeTypeName("size_t")] nuint filesize, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("const openmpt_module_initial_ctl *")] openmpt_module_initial_ctl* ctls);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern openmpt_module* openmpt_module_create_from_memory2([NativeTypeName("const void *")] void* filedata, [NativeTypeName("size_t")] nuint filesize, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser, int* error, [NativeTypeName("const char **")] sbyte** error_message, [NativeTypeName("const openmpt_module_initial_ctl *")] openmpt_module_initial_ctl* ctls);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_module_destroy(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_module_set_log_func(openmpt_module* mod, [NativeTypeName("openmpt_log_func")] delegate* unmanaged[Cdecl]<sbyte*, void*, void> logfunc, void* loguser);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_module_set_error_func(openmpt_module* mod, [NativeTypeName("openmpt_error_func")] delegate* unmanaged[Cdecl]<int, void*, int> errfunc, void* erruser);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_error_get_last(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_error_get_last_message(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_module_error_set_last(openmpt_module* mod, int error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void openmpt_module_error_clear(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_select_subsong(openmpt_module* mod, [NativeTypeName("int32_t")] int subsong);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_selected_subsong(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_set_repeat_count(openmpt_module* mod, [NativeTypeName("int32_t")] int repeat_count);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_repeat_count(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_get_duration_seconds(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_set_position_seconds(openmpt_module* mod, double seconds);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_get_position_seconds(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_set_position_order_row(openmpt_module* mod, [NativeTypeName("int32_t")] int order, [NativeTypeName("int32_t")] int row);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_get_render_param(openmpt_module* mod, int param1, [NativeTypeName("int32_t *")] int* value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_set_render_param(openmpt_module* mod, int param1, [NativeTypeName("int32_t")] int value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_mono(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, [NativeTypeName("int16_t *")] short* mono);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_stereo(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, [NativeTypeName("int16_t *")] short* left, [NativeTypeName("int16_t *")] short* right);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_quad(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, [NativeTypeName("int16_t *")] short* left, [NativeTypeName("int16_t *")] short* right, [NativeTypeName("int16_t *")] short* rear_left, [NativeTypeName("int16_t *")] short* rear_right);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_float_mono(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, float* mono);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_float_stereo(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, float* left, float* right);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_float_quad(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, float* left, float* right, float* rear_left, float* rear_right);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_interleaved_stereo(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, [NativeTypeName("int16_t *")] short* interleaved_stereo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_interleaved_quad(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, [NativeTypeName("int16_t *")] short* interleaved_quad);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_interleaved_float_stereo(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, float* interleaved_stereo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("size_t")]
    public static extern nuint openmpt_module_read_interleaved_float_quad(openmpt_module* mod, [NativeTypeName("int32_t")] int samplerate, [NativeTypeName("size_t")] nuint count, float* interleaved_quad);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_metadata_keys(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_metadata(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* key);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_get_current_estimated_bpm(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_current_speed(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    [Obsolete]
    public static extern int openmpt_module_get_current_tempo(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_get_current_tempo2(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_current_order(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_current_pattern(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_current_row(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_current_playing_channels(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float openmpt_module_get_current_channel_vu_mono(openmpt_module* mod, [NativeTypeName("int32_t")] int channel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float openmpt_module_get_current_channel_vu_left(openmpt_module* mod, [NativeTypeName("int32_t")] int channel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float openmpt_module_get_current_channel_vu_right(openmpt_module* mod, [NativeTypeName("int32_t")] int channel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float openmpt_module_get_current_channel_vu_rear_left(openmpt_module* mod, [NativeTypeName("int32_t")] int channel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern float openmpt_module_get_current_channel_vu_rear_right(openmpt_module* mod, [NativeTypeName("int32_t")] int channel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_subsongs(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_channels(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_orders(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_patterns(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_instruments(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_num_samples(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_subsong_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_channel_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_order_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_pattern_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_instrument_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_sample_name(openmpt_module* mod, [NativeTypeName("int32_t")] int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_order_pattern(openmpt_module* mod, [NativeTypeName("int32_t")] int order);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int32_t")]
    public static extern int openmpt_module_get_pattern_num_rows(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("uint8_t")]
    public static extern byte openmpt_module_get_pattern_row_channel_command(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern, [NativeTypeName("int32_t")] int row, [NativeTypeName("int32_t")] int channel, int command);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_format_pattern_row_channel_command(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern, [NativeTypeName("int32_t")] int row, [NativeTypeName("int32_t")] int channel, int command);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_highlight_pattern_row_channel_command(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern, [NativeTypeName("int32_t")] int row, [NativeTypeName("int32_t")] int channel, int command);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_format_pattern_row_channel(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern, [NativeTypeName("int32_t")] int row, [NativeTypeName("int32_t")] int channel, [NativeTypeName("size_t")] nuint width, int pad);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_highlight_pattern_row_channel(openmpt_module* mod, [NativeTypeName("int32_t")] int pattern, [NativeTypeName("int32_t")] int row, [NativeTypeName("int32_t")] int channel, [NativeTypeName("size_t")] nuint width, int pad);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_get_ctls(openmpt_module* mod);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    [Obsolete]
    public static extern sbyte* openmpt_module_ctl_get(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_ctl_get_boolean(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("int64_t")]
    public static extern nint openmpt_module_ctl_get_integer(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double openmpt_module_ctl_get_floatingpoint(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* openmpt_module_ctl_get_text(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [Obsolete]
    public static extern int openmpt_module_ctl_set(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl, [NativeTypeName("const char *")] sbyte* value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_ctl_set_boolean(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl, int value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_ctl_set_integer(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl, [NativeTypeName("int64_t")] nint value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_ctl_set_floatingpoint(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl, double value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int openmpt_module_ctl_set_text(openmpt_module* mod, [NativeTypeName("const char *")] sbyte* ctl, [NativeTypeName("const char *")] sbyte* value);

    [NativeTypeName("#define OPENMPT_STRING_LIBRARY_VERSION \"library_version\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_LIBRARY_VERSION => "library_version"u8;

    [NativeTypeName("#define OPENMPT_STRING_LIBRARY_FEATURES \"library_features\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_LIBRARY_FEATURES => "library_features"u8;

    [NativeTypeName("#define OPENMPT_STRING_CORE_VERSION \"core_version\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_CORE_VERSION => "core_version"u8;

    [NativeTypeName("#define OPENMPT_STRING_BUILD \"build\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_BUILD => "build"u8;

    [NativeTypeName("#define OPENMPT_STRING_CREDITS \"credits\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_CREDITS => "credits"u8;

    [NativeTypeName("#define OPENMPT_STRING_CONTACT \"contact\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_CONTACT => "contact"u8;

    [NativeTypeName("#define OPENMPT_STRING_LICENSE \"license\"")]
    public static ReadOnlySpan<byte> OPENMPT_STRING_LICENSE => "license"u8;

    [NativeTypeName("#define OPENMPT_STREAM_SEEK_SET 0")]
    public const int OPENMPT_STREAM_SEEK_SET = 0;

    [NativeTypeName("#define OPENMPT_STREAM_SEEK_CUR 1")]
    public const int OPENMPT_STREAM_SEEK_CUR = 1;

    [NativeTypeName("#define OPENMPT_STREAM_SEEK_END 2")]
    public const int OPENMPT_STREAM_SEEK_END = 2;

    [NativeTypeName("#define OPENMPT_ERROR_OK 0")]
    public const int OPENMPT_ERROR_OK = 0;

    [NativeTypeName("#define OPENMPT_ERROR_BASE 256")]
    public const int OPENMPT_ERROR_BASE = 256;

    [NativeTypeName("#define OPENMPT_ERROR_UNKNOWN ( OPENMPT_ERROR_BASE +   1 )")]
    public const int OPENMPT_ERROR_UNKNOWN = (256 + 1);

    [NativeTypeName("#define OPENMPT_ERROR_EXCEPTION ( OPENMPT_ERROR_BASE +  11 )")]
    public const int OPENMPT_ERROR_EXCEPTION = (256 + 11);

    [NativeTypeName("#define OPENMPT_ERROR_OUT_OF_MEMORY ( OPENMPT_ERROR_BASE +  21 )")]
    public const int OPENMPT_ERROR_OUT_OF_MEMORY = (256 + 21);

    [NativeTypeName("#define OPENMPT_ERROR_RUNTIME ( OPENMPT_ERROR_BASE +  30 )")]
    public const int OPENMPT_ERROR_RUNTIME = (256 + 30);

    [NativeTypeName("#define OPENMPT_ERROR_RANGE ( OPENMPT_ERROR_BASE +  31 )")]
    public const int OPENMPT_ERROR_RANGE = (256 + 31);

    [NativeTypeName("#define OPENMPT_ERROR_OVERFLOW ( OPENMPT_ERROR_BASE +  32 )")]
    public const int OPENMPT_ERROR_OVERFLOW = (256 + 32);

    [NativeTypeName("#define OPENMPT_ERROR_UNDERFLOW ( OPENMPT_ERROR_BASE +  33 )")]
    public const int OPENMPT_ERROR_UNDERFLOW = (256 + 33);

    [NativeTypeName("#define OPENMPT_ERROR_LOGIC ( OPENMPT_ERROR_BASE +  40 )")]
    public const int OPENMPT_ERROR_LOGIC = (256 + 40);

    [NativeTypeName("#define OPENMPT_ERROR_DOMAIN ( OPENMPT_ERROR_BASE +  41 )")]
    public const int OPENMPT_ERROR_DOMAIN = (256 + 41);

    [NativeTypeName("#define OPENMPT_ERROR_LENGTH ( OPENMPT_ERROR_BASE +  42 )")]
    public const int OPENMPT_ERROR_LENGTH = (256 + 42);

    [NativeTypeName("#define OPENMPT_ERROR_OUT_OF_RANGE ( OPENMPT_ERROR_BASE +  43 )")]
    public const int OPENMPT_ERROR_OUT_OF_RANGE = (256 + 43);

    [NativeTypeName("#define OPENMPT_ERROR_INVALID_ARGUMENT ( OPENMPT_ERROR_BASE +  44 )")]
    public const int OPENMPT_ERROR_INVALID_ARGUMENT = (256 + 44);

    [NativeTypeName("#define OPENMPT_ERROR_GENERAL ( OPENMPT_ERROR_BASE + 101 )")]
    public const int OPENMPT_ERROR_GENERAL = (256 + 101);

    [NativeTypeName("#define OPENMPT_ERROR_INVALID_MODULE_POINTER ( OPENMPT_ERROR_BASE + 102 )")]
    public const int OPENMPT_ERROR_INVALID_MODULE_POINTER = (256 + 102);

    [NativeTypeName("#define OPENMPT_ERROR_ARGUMENT_NULL_POINTER ( OPENMPT_ERROR_BASE + 103 )")]
    public const int OPENMPT_ERROR_ARGUMENT_NULL_POINTER = (256 + 103);

    [NativeTypeName("#define OPENMPT_ERROR_FUNC_RESULT_NONE 0")]
    public const int OPENMPT_ERROR_FUNC_RESULT_NONE = 0;

    [NativeTypeName("#define OPENMPT_ERROR_FUNC_RESULT_LOG ( 1 << 0 )")]
    public const int OPENMPT_ERROR_FUNC_RESULT_LOG = (1 << 0);

    [NativeTypeName("#define OPENMPT_ERROR_FUNC_RESULT_STORE ( 1 << 1 )")]
    public const int OPENMPT_ERROR_FUNC_RESULT_STORE = (1 << 1);

    [NativeTypeName("#define OPENMPT_ERROR_FUNC_RESULT_DEFAULT ( OPENMPT_ERROR_FUNC_RESULT_LOG | OPENMPT_ERROR_FUNC_RESULT_STORE )")]
    public const int OPENMPT_ERROR_FUNC_RESULT_DEFAULT = ((1 << 0) | (1 << 1));

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_FLAGS_MODULES 0x1ull")]
    public const ulong OPENMPT_PROBE_FILE_HEADER_FLAGS_MODULES = 0x1UL;

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_FLAGS_CONTAINERS 0x2ull")]
    public const ulong OPENMPT_PROBE_FILE_HEADER_FLAGS_CONTAINERS = 0x2UL;

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_FLAGS_DEFAULT ( OPENMPT_PROBE_FILE_HEADER_FLAGS_MODULES | OPENMPT_PROBE_FILE_HEADER_FLAGS_CONTAINERS )")]
    public const ulong OPENMPT_PROBE_FILE_HEADER_FLAGS_DEFAULT = (0x1UL | 0x2UL);

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_FLAGS_NONE 0x0ull")]
    public const ulong OPENMPT_PROBE_FILE_HEADER_FLAGS_NONE = 0x0UL;

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_RESULT_SUCCESS 1")]
    public const int OPENMPT_PROBE_FILE_HEADER_RESULT_SUCCESS = 1;

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_RESULT_FAILURE 0")]
    public const int OPENMPT_PROBE_FILE_HEADER_RESULT_FAILURE = 0;

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_RESULT_WANTMOREDATA (-1)")]
    public const int OPENMPT_PROBE_FILE_HEADER_RESULT_WANTMOREDATA = (-1);

    [NativeTypeName("#define OPENMPT_PROBE_FILE_HEADER_RESULT_ERROR (-255)")]
    public const int OPENMPT_PROBE_FILE_HEADER_RESULT_ERROR = (-255);

    [NativeTypeName("#define OPENMPT_MODULE_RENDER_MASTERGAIN_MILLIBEL 1")]
    public const int OPENMPT_MODULE_RENDER_MASTERGAIN_MILLIBEL = 1;

    [NativeTypeName("#define OPENMPT_MODULE_RENDER_STEREOSEPARATION_PERCENT 2")]
    public const int OPENMPT_MODULE_RENDER_STEREOSEPARATION_PERCENT = 2;

    [NativeTypeName("#define OPENMPT_MODULE_RENDER_INTERPOLATIONFILTER_LENGTH 3")]
    public const int OPENMPT_MODULE_RENDER_INTERPOLATIONFILTER_LENGTH = 3;

    [NativeTypeName("#define OPENMPT_MODULE_RENDER_VOLUMERAMPING_STRENGTH 4")]
    public const int OPENMPT_MODULE_RENDER_VOLUMERAMPING_STRENGTH = 4;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_NOTE 0")]
    public const int OPENMPT_MODULE_COMMAND_NOTE = 0;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_INSTRUMENT 1")]
    public const int OPENMPT_MODULE_COMMAND_INSTRUMENT = 1;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_VOLUMEEFFECT 2")]
    public const int OPENMPT_MODULE_COMMAND_VOLUMEEFFECT = 2;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_EFFECT 3")]
    public const int OPENMPT_MODULE_COMMAND_EFFECT = 3;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_VOLUME 4")]
    public const int OPENMPT_MODULE_COMMAND_VOLUME = 4;

    [NativeTypeName("#define OPENMPT_MODULE_COMMAND_PARAMETER 5")]
    public const int OPENMPT_MODULE_COMMAND_PARAMETER = 5;
}

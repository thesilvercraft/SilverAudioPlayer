using System.Runtime.InteropServices;
using SilverCraft.PortAudio;

namespace PortAudio;
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
public struct PaStream
{

}
public struct PaStreamCallbackFlags
{

}
public static unsafe partial class NativeMethods
{
    public const string DllName="libportaudio.so.2";
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int Pa_GetVersion();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* Pa_GetVersionText();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const PaVersionInfo *")]
    public static extern PaVersionInfo* Pa_GetVersionInfo();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern char* Pa_GetErrorText([NativeTypeName("PaError")] int errorCode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_Initialize();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_Terminate();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaHostApiIndex")]
    public static extern int Pa_GetHostApiCount();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaHostApiIndex")]
    public static extern int Pa_GetDefaultHostApi();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const PaHostApiInfo *")]
    public static extern PaHostApiInfo* Pa_GetHostApiInfo([NativeTypeName("PaHostApiIndex")] int hostApi);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaHostApiIndex")]
    public static extern int Pa_HostApiTypeIdToHostApiIndex(PaHostApiTypeId type);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaDeviceIndex")]
    public static extern int Pa_HostApiDeviceIndexToDeviceIndex([NativeTypeName("PaHostApiIndex")] int hostApi, int hostApiDeviceIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const PaHostErrorInfo *")]
    public static extern PaHostErrorInfo* Pa_GetLastHostErrorInfo();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaDeviceIndex")]
    public static extern int Pa_GetDeviceCount();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaDeviceIndex")]
    public static extern int Pa_GetDefaultInputDevice();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaDeviceIndex")]
    public static extern int Pa_GetDefaultOutputDevice();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const PaDeviceInfo *")]
    public static extern PaDeviceInfo* Pa_GetDeviceInfo([NativeTypeName("PaDeviceIndex")] int device);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_IsFormatSupported([NativeTypeName("const PaStreamParameters *")] PaStreamParameters* inputParameters, [NativeTypeName("const PaStreamParameters *")] PaStreamParameters* outputParameters, double sampleRate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_OpenStream([NativeTypeName("PaStream **")] PaStream** stream, [NativeTypeName("const PaStreamParameters *")] PaStreamParameters* inputParameters, [NativeTypeName("const PaStreamParameters *")] PaStreamParameters* outputParameters, double sampleRate, [NativeTypeName("unsigned long")] nuint framesPerBuffer, [NativeTypeName("PaStreamFlags")] nuint streamFlags, [NativeTypeName("PaStreamCallback *")] delegate* unmanaged[Cdecl]<void*, void*, ulong, PaStreamCallbackTimeInfo*, nuint, void*, int> streamCallback, void* userData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_OpenDefaultStream([NativeTypeName("PaStream **")] PaStream** stream, int numInputChannels, int numOutputChannels, [NativeTypeName("PaSampleFormat")] nuint sampleFormat, double sampleRate, [NativeTypeName("unsigned long")] nuint framesPerBuffer, [NativeTypeName("PaStreamCallback *")] StreamCallBack streamCallback, IntPtr userData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_CloseStream([NativeTypeName("PaStream *")] PaStream* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_SetStreamFinishedCallback([NativeTypeName("PaStream *")] void* stream, [NativeTypeName("PaStreamFinishedCallback *")] delegate* unmanaged[Cdecl]<void*, void> streamFinishedCallback);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_StartStream([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_StopStream([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_AbortStream([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_IsStreamStopped([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_IsStreamActive([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const PaStreamInfo *")]
    public static extern PaStreamInfo* Pa_GetStreamInfo([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaTime")]
    public static extern double Pa_GetStreamTime([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern double Pa_GetStreamCpuLoad([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_ReadStream([NativeTypeName("PaStream *")] void* stream, void* buffer, [NativeTypeName("unsigned long")] nuint frames);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_WriteStream([NativeTypeName("PaStream *")] void* stream, [NativeTypeName("const void *")] void* buffer, [NativeTypeName("unsigned long")] nuint frames);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("long")]
    public static extern nint Pa_GetStreamReadAvailable([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("long")]
    public static extern nint Pa_GetStreamWriteAvailable([NativeTypeName("PaStream *")] void* stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("PaError")]
    public static extern int Pa_GetSampleSize([NativeTypeName("PaSampleFormat")] nuint format);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void Pa_Sleep([NativeTypeName("long")] nint msec);

    [NativeTypeName("#define paNoDevice ((PaDeviceIndex)-1)")]
    public const int paNoDevice = ((int)(-1));

    [NativeTypeName("#define paUseHostApiSpecificDeviceSpecification ((PaDeviceIndex)-2)")]
    public const int paUseHostApiSpecificDeviceSpecification = ((int)(-2));

    [NativeTypeName("#define paFloat32 ((PaSampleFormat) 0x00000001)")]
    public const nuint paFloat32 = ((nuint)(0x00000001));

    [NativeTypeName("#define paInt32 ((PaSampleFormat) 0x00000002)")]
    public const nuint paInt32 = ((nuint)(0x00000002));

    [NativeTypeName("#define paInt24 ((PaSampleFormat) 0x00000004)")]
    public const nuint paInt24 = ((nuint)(0x00000004));

    [NativeTypeName("#define paInt16 ((PaSampleFormat) 0x00000008)")]
    public const nuint paInt16 = ((nuint)(0x00000008));

    [NativeTypeName("#define paInt8 ((PaSampleFormat) 0x00000010)")]
    public const nuint paInt8 = ((nuint)(0x00000010));

    [NativeTypeName("#define paUInt8 ((PaSampleFormat) 0x00000020)")]
    public const nuint paUInt8 = ((nuint)(0x00000020));

    [NativeTypeName("#define paCustomFormat ((PaSampleFormat) 0x00010000)")]
    public const nuint paCustomFormat = ((nuint)(0x00010000));

    [NativeTypeName("#define paNonInterleaved ((PaSampleFormat) 0x80000000)")]
    public const nuint paNonInterleaved = ((nuint)(0x80000000));

    [NativeTypeName("#define paFormatIsSupported (0)")]
    public const int paFormatIsSupported = (0);

    [NativeTypeName("#define paFramesPerBufferUnspecified (0)")]
    public const int paFramesPerBufferUnspecified = (0);

    [NativeTypeName("#define paNoFlag ((PaStreamFlags) 0)")]
    public const nuint paNoFlag = ((nuint)(0));

    [NativeTypeName("#define paClipOff ((PaStreamFlags) 0x00000001)")]
    public const nuint paClipOff = ((nuint)(0x00000001));

    [NativeTypeName("#define paDitherOff ((PaStreamFlags) 0x00000002)")]
    public const nuint paDitherOff = ((nuint)(0x00000002));

    [NativeTypeName("#define paNeverDropInput ((PaStreamFlags) 0x00000004)")]
    public const nuint paNeverDropInput = ((nuint)(0x00000004));

    [NativeTypeName("#define paPrimeOutputBuffersUsingStreamCallback ((PaStreamFlags) 0x00000008)")]
    public const nuint paPrimeOutputBuffersUsingStreamCallback = ((nuint)(0x00000008));

    [NativeTypeName("#define paPlatformSpecificFlags ((PaStreamFlags)0xFFFF0000)")]
    public const nuint paPlatformSpecificFlags = ((nuint)(0xFFFF0000));

    [NativeTypeName("#define paInputUnderflow ((PaStreamCallbackFlags) 0x00000001)")]
    public const nuint paInputUnderflow = ((nuint)(0x00000001));

    [NativeTypeName("#define paInputOverflow ((PaStreamCallbackFlags) 0x00000002)")]
    public const nuint paInputOverflow = ((nuint)(0x00000002));

    [NativeTypeName("#define paOutputUnderflow ((PaStreamCallbackFlags) 0x00000004)")]
    public const nuint paOutputUnderflow = ((nuint)(0x00000004));

    [NativeTypeName("#define paOutputOverflow ((PaStreamCallbackFlags) 0x00000008)")]
    public const nuint paOutputOverflow = ((nuint)(0x00000008));

    [NativeTypeName("#define paPrimingOutput ((PaStreamCallbackFlags) 0x00000010)")]
    public const nuint paPrimingOutput = ((nuint)(0x00000010));
}

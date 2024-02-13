using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSCore;
using CSCore.SoundOut;
using PortAudio;

namespace SilverCraft.PortAudio;


[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int StreamCallBack(void* input, void* output, ulong frameCount, PaStreamCallbackTimeInfo* timeInfo, PaStreamCallbackFlags statusFlags, IntPtr userData);

public sealed class CallbackHelper
{
    public CallbackHelper(StreamCallBack callBack)
    {
        CallBack = callBack;
    }
    public StreamCallBack CallBack { get; set; }
}
//https://www.portaudio.com/docs/v19-doxydocs/writing_a_callback.html
public class PortAudioSoundOut : ISoundOut
{
    public PortAudioSoundOut()
    {

    }
    public PortAudioSoundOut(ushort bufferLength) : this()
    {
        Buffer = bufferLength;
    }
    public float Volume { get; set; }

    public IWaveSource? WaveSource { get; set; }
    ISampleSource? SampleSource { get; set; }
    public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;

    public event EventHandler<PlaybackStoppedEventArgs>? Stopped;
    public static bool IsSupported()
    {
        try
        {
            _ = NativeMethods.Pa_GetVersion();
            return true;
        }
        catch
        {
            return false;
        }
    }
    public void Dispose()
    {
        if (!isPAInit)
        {
            var err = NativeMethods.Pa_Terminate();
            if (err != (int)PaErrorCode.paNoError)
            {
                throw new Exception($"PortAudio failed to terminate, error code {err}");
            }
            c = null;
            isPAInit = false;
        }
        GC.SuppressFinalize(this);
    }
    void OnStop()
    {
        PlaybackState = PlaybackState.Stopped;
        Task.Run(() =>
        {
            unsafe
            {
                ThrowIfError(NativeMethods.Pa_CloseStream(stream));
                stream = null;
            }
            Stopped?.Invoke(this, new());
        });

    }
    unsafe PaStream* stream;
    public static bool isPAInit = false;
    CallbackHelper? c;
    unsafe void ThrowIfError(int err, [CallerMemberName] string callerName = "", [CallerFilePath] string path = "", [CallerLineNumber] int num = 0)
    {
        if (err != (int)PaErrorCode.paNoError)
        {
            throw new Exception($"PortAudio failed, error code {err} ({Marshal.PtrToStringAnsi((IntPtr)NativeMethods.Pa_GetErrorText(err))}) in method {callerName} in {path}:{num}");
        }
    }
    public ushort Buffer { get; set; } = 50;
    public void Initialize(IWaveSource source)
    {
        if (!isPAInit)
        {
            ThrowIfError(NativeMethods.Pa_Initialize());
            unsafe
            {
                c = new(CallBackMethod);
            }
            isPAInit = true;
        }
        WaveSource?.Dispose();
        SampleSource?.Dispose();
        WaveSource = source;
        SampleSource = source.ToSampleSource();
        unsafe
        {
            PaStream* stream;
            ThrowIfError(NativeMethods.Pa_OpenDefaultStream(&stream,
                                        0,
                                         SampleSource.WaveFormat.Channels,
                                        NativeMethods.paFloat32,
                                        SampleSource.WaveFormat.SampleRate,
                                        (nuint)(Buffer / 1000d * WaveSource.WaveFormat.SampleRate),//frames per buffer      
                                        c.CallBack,
                                        0));
            this.stream = stream;
        }
        channels = SampleSource.WaveFormat.Channels;
    }
    int channels;
    private unsafe int CallBackMethod(void* input, void* output, ulong frameCount, PaStreamCallbackTimeInfo* timeInfo, PaStreamCallbackFlags statusFlags, nint userData)
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        float* outputBuffer = (float*)output;
        int bufferLength = (int)frameCount * channels;
        float[] buffer = new float[bufferLength];
        var r = SampleSource?.Read(buffer, 0, bufferLength);
        r ??= 0;

        fixed (float* bufferStart = buffer)
        {
            float* bufferI = bufferStart;
            float* bufferEnd = bufferStart + r.Value;
            while (bufferI < bufferEnd)
            {
                *outputBuffer++ = *bufferI++*Volume;
            }
        }

        if (r < buffer.Length)
        {
            OnStop();
            return (int)PaStreamCallbackResult.paComplete;
        }
        return 0;
    }

    public unsafe void Pause()
    {
        if (PlaybackState == PlaybackState.Playing)
        {
            ThrowIfError(NativeMethods.Pa_StopStream(stream));
            PlaybackState = PlaybackState.Paused;
        }
    }

    public unsafe void Play()
    {
        if (PlaybackState != PlaybackState.Playing)
        {
            ThrowIfError(NativeMethods.Pa_StartStream(stream));
            PlaybackState = PlaybackState.Playing;
        }
    }

    public unsafe void Resume()
    {
        if (PlaybackState == PlaybackState.Paused)
        {
            ThrowIfError(NativeMethods.Pa_StartStream(stream));
            PlaybackState = PlaybackState.Playing;
        }
    }

    public unsafe void Stop()
    {
        if (PlaybackState != PlaybackState.Stopped)
        {
            ThrowIfError(NativeMethods.Pa_StopStream(stream));
            OnStop();
        }
    }
}

using System.Diagnostics;
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
    [Conditional("DEBUG")]
    void DebugTrace(string message = null,  [CallerMemberName] string method = null)
    {
        Debug.WriteLine($"PORTAUDIO {method} {message}");
    }
    public void Dispose()
    {
        DebugTrace();
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
        DebugTrace();

        PlaybackState = PlaybackState.Stopped;
        Task.Run(() =>
        {
            unsafe
            {
                if(stream!=null)
                {
                    ThrowIfError(NativeMethods.Pa_CloseStream(stream));
                }
                stream = null;
                buffer = null;
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
    public unsafe void Initialize(IWaveSource source)
    {
        DebugTrace();

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
                                        0,
                                        c.CallBack,
                                        0));
            this.stream = stream;
        }
        channels = SampleSource.WaveFormat.Channels;
    }
    int channels;
    float[]? buffer = new float[2];
    private unsafe int CallBackMethod(void* input, void* output, ulong frameCount, PaStreamCallbackTimeInfo* timeInfo, PaStreamCallbackFlags statusFlags, nint userData)
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        var outputBuffer = (float*)output;
        var bufferLength = (int)frameCount * channels;
        if (buffer == null || buffer.Length < bufferLength)
        {
            buffer = new float[bufferLength];
        }
        var r = SampleSource?.Read(buffer, 0, bufferLength);
        r ??= 0;

        fixed (float* bufferStart = buffer)
        {
            var bufferI = bufferStart;
            var bufferEnd = bufferStart + r.Value;
            while (bufferI < bufferEnd)
            {
                *outputBuffer++ = *bufferI++ * Volume;
            }
        }

        if (!(r < buffer.Length)) return 0;
        OnStop();
        return (int)PaStreamCallbackResult.paComplete;
    }
    public unsafe void MakeSureStreamIsNotNull(PaStream* stream)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));
    }
    

    public unsafe void Play()
    {
        DebugTrace();
        if (PlaybackState == PlaybackState.Playing) return;
        MakeSureStreamIsNotNull(stream);
        Task.Run(() => ThrowIfError(NativeMethods.Pa_StartStream(stream)));
        PlaybackState = PlaybackState.Playing;
    }

    public unsafe void Resume()
    {
        if (PlaybackState != PlaybackState.Paused) return;
        Play();
    }
    public unsafe void Pause()
    {
        DebugTrace();

        if (PlaybackState != PlaybackState.Playing) return;
        MakeSureStreamIsNotNull(stream);
        ThrowIfError(NativeMethods.Pa_StopStream(stream));
        PlaybackState = PlaybackState.Paused;
    }
    public unsafe void Stop()
    {
        if (PlaybackState == PlaybackState.Stopped) return;
        DebugTrace();
        if (PlaybackState == PlaybackState.Paused)
        {
            PlaybackState = PlaybackState.Stopped;
            OnStop();
            return;
        }
        MakeSureStreamIsNotNull(stream);
        ThrowIfError(NativeMethods.Pa_StopStream(stream));
        OnStop();
    }
}

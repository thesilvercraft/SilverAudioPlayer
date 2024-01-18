using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore;
using CSCore.SoundOut;
using PortAudio;

namespace SilverCraft.PortAudio;


/*typedef int PaStreamCallback( const void *input,
                                      void *output,
                                      ,
                                      const PaStreamCallbackTimeInfo* timeInfo,
                                      PaStreamCallbackFlags statusFlags,
                                      void *userData ) ;
*/
// unmanaged[Cdecl]<void*, void*, nuint, PaStreamCallbackTimeInfo*, nuint, void*, int>
//delegate* unmanaged[Cdecl]<void*, void*, ulong, PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, IntPtr, int>
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
    public float Volume { get => 1; set => Debug.WriteLine("TODO"); }

    public IWaveSource WaveSource { get; set; }
    ISampleSource SampleSource { get; set; }

    public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;

    public event EventHandler<PlaybackStoppedEventArgs> Stopped;

    public void Dispose()
    {
        if (!isPAInit)
        {
            var err = NativeMethods.Pa_Terminate();
            if (err != (int)PaErrorCode.paNoError)
            {
                throw new Exception($"PortAudio failed to terminate, error code {err}");
            }
            isPAInit = false;
        }
    }
    void OnStop()
    {
        Stopped.Invoke(this, new());
        Task.Run(() =>
        {
            unsafe
            {
                NativeMethods.Pa_CloseStream(stream);
                stream = null;
            }
            WaveSource=null;
            SampleSource=null;
            c = null;
        });

    }
    unsafe PaStream* stream;
    public static bool isPAInit = false;
    CallbackHelper c;
    public void Initialize(IWaveSource source)
    {
        int err;
        if (!isPAInit)
        {
            err = NativeMethods.Pa_Initialize();
            if (err != (int)PaErrorCode.paNoError)
            {
                throw new Exception($"PortAudio failed to initialise, error code {err}");
            }
            isPAInit = true;
        }
        WaveSource = source;
        SampleSource = source.ToSampleSource();
        unsafe
        {
            PaStream* stream;
            c = new(CallBackMethod);
            err = NativeMethods.Pa_OpenDefaultStream(&stream,
                                        0,
                                         SampleSource.WaveFormat.Channels,
                                        NativeMethods.paFloat32,
                                        SampleSource.WaveFormat.SampleRate,
                                        NativeMethods.paFramesPerBufferUnspecified,//frames per buffer      
                                        c.CallBack,
                                        0);
            this.stream = stream;
            if (err != (int)PaErrorCode.paNoError)
            {
                throw new Exception($"PortAudio failed to create stream, error code {err}");
            }
        }
        channels = SampleSource.WaveFormat.Channels;
    }
    int channels;
    private unsafe int CallBackMethod(void* input, void* output, ulong frameCount, PaStreamCallbackTimeInfo* timeInfo, PaStreamCallbackFlags statusFlags, nint userData)
    {
        float* outputBuffer = (float*)output;
        int i;
        //fun shit time
        float[] buffer = new float[(uint)frameCount * channels];
        var r = SampleSource.Read(buffer, 0, buffer.Length);
        for (i = 0; i < (int)frameCount * channels; i++)
        {
            *(outputBuffer++) = buffer[i];
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
        var err = NativeMethods.Pa_StopStream(stream);
        PlaybackState = PlaybackState.Paused;

    }

    public unsafe void Play()
    {
        var err = NativeMethods.Pa_StartStream(stream);
        PlaybackState = PlaybackState.Playing;
    }

    public unsafe void Resume()
    {
        var err = NativeMethods.Pa_StartStream(stream);
        PlaybackState = PlaybackState.Playing;

    }

    public unsafe void Stop()
    {
        var err = NativeMethods.Pa_StopStream(stream);
        PlaybackState = PlaybackState.Stopped;
        OnStop();
    }
}

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;
using SilverCraft.CSCore;
using SilverCraft.CSCore.PortAudio;
using SilverCraft.CSCore.PortAudio.Native;
using SilverCraft.CSCore.SoundOut;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

/// <summary>
/// PortAudio <see cref="ISoundOut"/> implementation that decouples decoding from the real-time
/// audio callback using an SPSC ring buffer and opens PortAudio via <c>Pa_OpenStream</c>
/// with configurable output device and latency.
/// </summary>
public class SapPortAudioSoundOut : ISoundOut
{
    public const string DefaultDeviceLabel = "Default";

    private readonly Lock _streamLock = new();
    private readonly Lock _sourceReadLock = new();
    private readonly string? _preferredDevice;
    private readonly double _targetLatencySeconds;
    private readonly int _decodeBufferMs;

    private StreamCallBack? _callback;
    private PaStreamFinishedCallback? _finishedCallback;
    private unsafe PaStream* _stream;
    private int _currentStreamId;
    private bool _isDisposed;
    private bool _isPaInit;

    private ISampleSource? _sampleSource;
    private int _channels;
    private int _sampleRate;
    private float _volume = 1f;
    private double _streamOutputLatencySeconds;

    // SPSC ring buffer (power-of-two capacity)
    private float[] _ringBuffer = [];
    private int _ringMask;
    private long _writePos;
    private long _readPos;
    private volatile bool _endOfStream;
    private int _isFlushing;

    private Thread? _producerThread;
    private CancellationTokenSource? _producerCts;
    private readonly AutoResetEvent _spaceAvailable = new(false);

    public SapPortAudioSoundOut(
        string? preferredDevice = null,
        TimeSpan? targetLatency = null,
        TimeSpan? decodeBuffer = null)
    {
        _preferredDevice = preferredDevice;
        _targetLatencySeconds = Math.Clamp((targetLatency ?? TimeSpan.FromMilliseconds(50)).TotalSeconds, 0.01, 1.0);
        _decodeBufferMs = Math.Clamp((int)(decodeBuffer ?? TimeSpan.FromMilliseconds(100)).TotalMilliseconds, 20, 1000);
    }

    public float Volume
    {
        get => Volatile.Read(ref _volume);
        set
        {
            if (value is < 0f or > 1f) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _volume, value);
        }
    }

    public IWaveSource WaveSource { get; private set; } = null!;
    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

    public event EventHandler<PlaybackStoppedEventArgs>? Stopped;

    /// <summary>
    /// Total unplayed audio latency currently buffered in the ring buffer and PortAudio stream.
    /// </summary>
    public TimeSpan OutputLatency
    {
        get
        {
            if (_sampleRate <= 0 || _channels <= 0 || Volatile.Read(ref _isFlushing) != 0)
            {
                return TimeSpan.FromSeconds(_streamOutputLatencySeconds);
            }

            var bufferedSamples = Math.Max(0L, Volatile.Read(ref _writePos) - Volatile.Read(ref _readPos));
            var bufferedSeconds = (double)bufferedSamples / (_sampleRate * _channels);
            return TimeSpan.FromSeconds(bufferedSeconds + _streamOutputLatencySeconds);
        }
    }

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

    /// <summary>
    /// Enumerates available PortAudio output devices formatted as <c>DeviceName (HostApiName)</c>,
    /// prefixed with <see cref="DefaultDeviceLabel"/>.
    /// </summary>
    public static unsafe string[] GetOutputDeviceNames()
    {
        var result = new List<string> { DefaultDeviceLabel };
        try
        {
            PortAudioLifetimeManager.Initialize();
            var count = NativeMethods.Pa_GetDeviceCount();
            for (var i = 0; i < count; i++)
            {
                var info = NativeMethods.Pa_GetDeviceInfo(i);
                if (info == null || info->maxOutputChannels <= 0) continue;

                var deviceName = Marshal.PtrToStringAnsi((nint)info->name) ?? $"Device {i}";
                var hostInfo = NativeMethods.Pa_GetHostApiInfo(info->hostApi);
                var hostName = hostInfo != null
                    ? Marshal.PtrToStringAnsi((nint)hostInfo->name) ?? "Unknown"
                    : "Unknown";

                var label = $"{deviceName} ({hostName})";
                if (!result.Contains(label))
                {
                    result.Add(label);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate PortAudio output devices");
        }

        return [.. result];
    }

    private static unsafe int ResolveDeviceIndex(string? preferredDevice)
    {
        var defaultDevice = NativeMethods.Pa_GetDefaultOutputDevice();
        if (string.IsNullOrWhiteSpace(preferredDevice) ||
            string.Equals(preferredDevice, DefaultDeviceLabel, StringComparison.OrdinalIgnoreCase))
        {
            return defaultDevice;
        }

        var count = NativeMethods.Pa_GetDeviceCount();
        for (var i = 0; i < count; i++)
        {
            var info = NativeMethods.Pa_GetDeviceInfo(i);
            if (info == null || info->maxOutputChannels <= 0) continue;

            var deviceName = Marshal.PtrToStringAnsi((nint)info->name) ?? $"Device {i}";
            var hostInfo = NativeMethods.Pa_GetHostApiInfo(info->hostApi);
            var hostName = hostInfo != null
                ? Marshal.PtrToStringAnsi((nint)hostInfo->name) ?? "Unknown"
                : "Unknown";

            if (string.Equals($"{deviceName} ({hostName})", preferredDevice, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(deviceName, preferredDevice, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        Log.Warning("Configured PortAudio device '{Device}' not found, falling back to default device", preferredDevice);
        return defaultDevice;
    }

    public unsafe void Initialize(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_isPaInit)
        {
            PortAudioLifetimeManager.Initialize();
            _isPaInit = true;
        }

        CloseActiveStreamInternal();

        if (source != WaveSource) WaveSource?.Dispose();
        _sampleSource?.Dispose();

        WaveSource = source;
        _sampleSource = source.ToSampleSource();
        _channels = _sampleSource.WaveFormat.Channels;
        _sampleRate = _sampleSource.WaveFormat.SampleRate;

        // Allocate power-of-two ring buffer sized to _decodeBufferMs
        var minSamples = Math.Max((int)((long)_sampleRate * _channels * _decodeBufferMs / 1000), 8192);
        var capacity = 1;
        while (capacity < minSamples) capacity <<= 1;
        _ringBuffer = new float[capacity];
        _ringMask = capacity - 1;
        _writePos = 0;
        _readPos = 0;
        _endOfStream = false;
        Volatile.Write(ref _isFlushing, 0);

        _callback = CallBackMethod;
        _finishedCallback = OnStreamFinished;

        var newStreamId = Interlocked.Increment(ref _currentStreamId);
        var deviceIndex = ResolveDeviceIndex(_preferredDevice);
        var deviceInfo = NativeMethods.Pa_GetDeviceInfo(deviceIndex);
        if (deviceInfo == null)
        {
            throw new PortAudioException($"PortAudio failed to get info for output device index {deviceIndex}");
        }

        var suggestedLatency = Math.Max(deviceInfo->defaultHighOutputLatency, _targetLatencySeconds);
        var outputChannels = _channels;
        if (deviceInfo->maxOutputChannels > 0 && outputChannels > deviceInfo->maxOutputChannels)
        {
            outputChannels = deviceInfo->maxOutputChannels;
        }

        var outputParams = new PaStreamParameters
        {
            device = deviceIndex,
            channelCount = outputChannels,
            sampleFormat = NativeMethods.paFloat32,
            suggestedLatency = suggestedLatency,
            hostApiSpecificStreamInfo = null
        };

        var callbackPtr = (delegate* unmanaged[Cdecl]<void*, void*, ulong, PaStreamCallbackTimeInfo*, nuint, void*, int>)
            Marshal.GetFunctionPointerForDelegate(_callback);

        PaStream* paStream;
        ThrowIfError(NativeMethods.Pa_OpenStream(
            &paStream,
            null,
            &outputParams,
            _sampleRate,
            0,
            NativeMethods.paNoFlag,
            callbackPtr,
            (void*)(nint)newStreamId));

        ThrowIfError(NativeMethods.Pa_SetStreamFinishedCallback(paStream, _finishedCallback));

        var streamInfo = NativeMethods.Pa_GetStreamInfo(paStream);
        _streamOutputLatencySeconds = streamInfo != null ? streamInfo->outputLatency : suggestedLatency;

        lock (_streamLock)
        {
            _stream = paStream;
            PlaybackState = PlaybackState.Stopped;
        }

        StartProducerThread();
    }

    /// <summary>
    /// Executes a seek operation serialized against the decode producer thread and flushes
    /// stale samples from the ring buffer.
    /// </summary>
    public void ExecuteSeek(Action seekAction)
    {
        ArgumentNullException.ThrowIfNull(seekAction);
        Volatile.Write(ref _isFlushing, 1);
        try
        {
            lock (_sourceReadLock)
            {
                seekAction();
                Volatile.Write(ref _readPos, 0);
                Volatile.Write(ref _writePos, 0);
                _endOfStream = false;
            }
        }
        finally
        {
            Volatile.Write(ref _isFlushing, 0);
            _spaceAvailable.Set();
        }
    }

    private void StartProducerThread()
    {
        StopProducerThread();

        _producerCts = new CancellationTokenSource();
        var token = _producerCts.Token;
        _producerThread = new Thread(() => ProducerLoop(token))
        {
            Name = "SAP PortAudio Decode Buffer",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _producerThread.Start();
    }

    private void StopProducerThread()
    {
        if (_producerCts != null)
        {
            _producerCts.Cancel();
            _spaceAvailable.Set();
        }

        if (_producerThread != null && _producerThread != Thread.CurrentThread)
        {
            _producerThread.Join(TimeSpan.FromSeconds(2));
        }

        _producerCts?.Dispose();
        _producerCts = null;
        _producerThread = null;
    }

    private void ProducerLoop(CancellationToken token)
    {
        var chunkSize = Math.Max(_channels * 1024, 2048);
        var tempBuffer = new float[chunkSize];

        while (!token.IsCancellationRequested)
        {
            if (Volatile.Read(ref _isFlushing) != 0 || _endOfStream)
            {
                _spaceAvailable.WaitOne(15);
                continue;
            }

            var capacity = _ringBuffer.Length;
            if (capacity == 0)
            {
                _spaceAvailable.WaitOne(15);
                continue;
            }

            var writePos = Volatile.Read(ref _writePos);
            var readPos = Volatile.Read(ref _readPos);
            var freeSpace = capacity - (int)(writePos - readPos);

            if (freeSpace <= 0)
            {
                _spaceAvailable.WaitOne(10);
                continue;
            }

            var toRead = Math.Min(freeSpace, tempBuffer.Length);
            int read;
            lock (_sourceReadLock)
            {
                if (token.IsCancellationRequested || Volatile.Read(ref _isFlushing) != 0)
                {
                    continue;
                }

                var src = _sampleSource;
                if (src == null)
                {
                    _endOfStream = true;
                    continue;
                }

                try
                {
                    read = src.Read(tempBuffer, 0, toRead);
                }
                catch (ObjectDisposedException)
                {
                    _endOfStream = true;
                    continue;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error decoding audio in SapPortAudioSoundOut producer");
                    _endOfStream = true;
                    continue;
                }
            }

            if (read <= 0)
            {
                _endOfStream = true;
                continue;
            }

            // Copy decoded floats into circular ring buffer (SPSC: only producer advances _writePos)
            var idx = (int)(writePos & _ringMask);
            var firstPart = Math.Min(read, capacity - idx);
            Array.Copy(tempBuffer, 0, _ringBuffer, idx, firstPart);
            if (read > firstPart)
            {
                Array.Copy(tempBuffer, firstPart, _ringBuffer, 0, read - firstPart);
            }

            Volatile.Write(ref _writePos, writePos + read);
        }
    }

    public unsafe void Play()
    {
        if (PlaybackState == PlaybackState.Playing) return;

        PaStream* s;
        lock (_streamLock)
        {
            ObjectDisposedException.ThrowIf(_stream == null, nameof(_stream));
            PlaybackState = PlaybackState.Playing;
            s = _stream;
        }

        ThrowIfError(NativeMethods.Pa_StartStream(s));
    }

    public void Resume()
    {
        if (PlaybackState != PlaybackState.Paused) return;
        Play();
    }

    public unsafe void Pause()
    {
        if (PlaybackState != PlaybackState.Playing) return;

        PaStream* s;
        lock (_streamLock)
        {
            ObjectDisposedException.ThrowIf(_stream == null, nameof(_stream));
            PlaybackState = PlaybackState.Paused;
            s = _stream;
        }

        ThrowIfError(NativeMethods.Pa_StopStream(s));
    }

    public void Stop()
    {
        if (PlaybackState == PlaybackState.Stopped) return;

        CloseActiveStreamInternal();
        Stopped?.Invoke(this, new PlaybackStoppedEventArgs());
    }

    private unsafe void OnStreamFinished(void* userData)
    {
        var callbackStreamId = (int)(nint)userData;
        if (callbackStreamId != Volatile.Read(ref _currentStreamId)) return;
        if (PlaybackState != PlaybackState.Playing) return;

        Task.Run(OnStop);
    }

    private void OnStop()
    {
        if (PlaybackState == PlaybackState.Stopped) return;

        CloseActiveStreamInternal();
        Stopped?.Invoke(this, new PlaybackStoppedEventArgs());
    }

    private unsafe void CloseActiveStreamInternal()
    {
        PaStream* s;
        lock (_streamLock)
        {
            if (_stream == null)
            {
                PlaybackState = PlaybackState.Stopped;
                StopProducerThread();
                return;
            }

            s = _stream;
            _stream = null;
            PlaybackState = PlaybackState.Stopped;
        }

        NativeMethods.Pa_AbortStream(s);
        var closeResult = NativeMethods.Pa_CloseStream(s);
        if (closeResult != (int)PaErrorCode.paNoError)
        {
            Log.Error("PORTAUDIO ERROR: Pa_CloseStream failed with code {CloseResult}", closeResult);
        }

        StopProducerThread();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CloseActiveStreamInternal();

        if (disposing)
        {
            lock (_sourceReadLock)
            {
                _sampleSource?.Dispose();
                _sampleSource = null;
                WaveSource?.Dispose();
                WaveSource = null;
            }

            _ringBuffer = [];
            _spaceAvailable.Dispose();
        }

        _callback = null;
        _finishedCallback = null;

        if (!_isPaInit) return;
        PortAudioLifetimeManager.Terminate(null);
        _isPaInit = false;
    }

    ~SapPortAudioSoundOut()
    {
        Dispose(false);
    }

    private static unsafe void ThrowIfError(int err, [CallerMemberName] string callerName = "",
        [CallerFilePath] string path = "", [CallerLineNumber] int num = 0)
    {
        if (err != (int)PaErrorCode.paNoError)
            throw new PortAudioException(
                $"PortAudio failed, error code {err} ({Marshal.PtrToStringAnsi((nint)NativeMethods.Pa_GetErrorText(err))}) in method {callerName} in {path}:{num}");
    }

    private unsafe int CallBackMethod(void* input, void* output, ulong frameCount, PaStreamCallbackTimeInfo* timeInfo,
        PaStreamCallbackFlags statusFlags, nint userData)
    {
        try
        {
            var callbackStreamId = (int)userData;
            if (callbackStreamId != Volatile.Read(ref _currentStreamId))
            {
                return (int)PaStreamCallbackResult.paAbort;
            }

            var outputBuffer = (float*)output;
            if (frameCount > (ulong)(int.MaxValue / Math.Max(_channels, 1)))
            {
                return (int)PaStreamCallbackResult.paAbort;
            }

            var samplesRequested = (int)frameCount * _channels;

            if (Volatile.Read(ref _isFlushing) != 0 || _ringBuffer.Length == 0)
            {
                new Span<float>(outputBuffer, samplesRequested).Clear();
                return (int)PaStreamCallbackResult.paContinue;
            }

            var writePos = Volatile.Read(ref _writePos);
            var readPos = Volatile.Read(ref _readPos);
            var available = (int)(writePos - readPos);
            var toRead = Math.Min(available, samplesRequested);

            if (toRead > 0)
            {
                var capacity = _ringBuffer.Length;
                var idx = (int)(readPos & _ringMask);
                var firstPart = Math.Min(toRead, capacity - idx);
                var vol = Volatile.Read(ref _volume);

                fixed (float* ringPtr = _ringBuffer)
                {
                    Unsafe.CopyBlockUnaligned(outputBuffer, ringPtr + idx, (uint)(firstPart * sizeof(float)));
                    if (toRead > firstPart)
                    {
                        Unsafe.CopyBlockUnaligned(outputBuffer + firstPart, ringPtr,
                            (uint)((toRead - firstPart) * sizeof(float)));
                    }
                }

                if (vol != 1f)
                {
                    for (var i = 0; i < toRead; i++)
                    {
                        outputBuffer[i] *= vol;
                    }
                }

                Volatile.Write(ref _readPos, readPos + toRead);
                _spaceAvailable.Set();
            }

            if (toRead < samplesRequested)
            {
                new Span<float>(outputBuffer + toRead, samplesRequested - toRead).Clear();
                if (_endOfStream && available == toRead)
                {
                    return (int)PaStreamCallbackResult.paComplete;
                }
            }

            return (int)PaStreamCallbackResult.paContinue;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SapPortAudioSoundOut callback thread");
            return (int)PaStreamCallbackResult.paAbort;
        }
    }
}

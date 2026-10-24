using NAudio.Wave;
using SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class GaplessWaveProvider : IWaveStream, IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<IWaveProvider> _queuedProviders = new();
    private IWaveProvider _currentProvider;
    private long _positionBase;
    private long _length;
    private bool _isDisposed;

    public GaplessWaveProvider(IWaveProvider initialProvider)
    {
        ArgumentNullException.ThrowIfNull(initialProvider);
        _currentProvider = initialProvider;
        WaveFormat = initialProvider.WaveFormat;
        _length = GetLength(initialProvider);
    }

    public WaveFormat WaveFormat { get; }

    public long Position
    {
        get
        {
            lock (_lock)
            {
                var currentPosition = GetCurrentPosition(_currentProvider);
                return currentPosition < 0 ? 0 : _positionBase + currentPosition;
            }
        }
        set => throw new NotSupportedException();
    }

    public long Length
    {
        get
        {
            lock (_lock)
            {
                return _length;
            }
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var hasProviderChanged = false;
        var totalRead = 0;

        lock (_lock)
        {
            if (_isDisposed)
            {
                return 0;
            }

            while (count > 0)
            {
                var read = 0;
                try
                {
                    read = _currentProvider.Read(buffer, offset, count);
                }
                catch (Exception)
                {
                    read = 0;
                }

                if (read > 0)
                {
                    totalRead += read;
                    offset += read;
                    count -= read;
                    continue;
                }

                if (!SwitchToNextProvider(out var changedThisIter))
                {
                    break;
                }

                if (changedThisIter)
                {
                    hasProviderChanged = true;
                }
            }
        }

        if (hasProviderChanged)
        {
            CurrentProviderChanged?.Invoke(this, EventArgs.Empty);
        }

        return totalRead;
    }

    public event EventHandler? CurrentProviderChanged;

    public IWaveProvider CurrentProvider
    {
        get
        {
            lock (_lock)
            {
                return _currentProvider;
            }
        }
    }

    public bool Enqueue(IWaveProvider nextProvider)
    {
        ArgumentNullException.ThrowIfNull(nextProvider);

        if (!WaveFormat.Equals(nextProvider.WaveFormat))
        {
            return false;
        }

        lock (_lock)
        {
            if (_isDisposed) return false;

            _queuedProviders.Enqueue(nextProvider);
            var nextLength = GetLength(nextProvider);
            if (_length >= 0 && nextLength >= 0)
            {
                _length += nextLength;
            }
            else
            {
                _length = -1;
            }
        }

        return true;
    }

    public bool ReplaceNextProvider(IWaveProvider nextProvider)
    {
        ArgumentNullException.ThrowIfNull(nextProvider);

        if (!WaveFormat.Equals(nextProvider.WaveFormat))
        {
            return false;
        }

        lock (_lock)
        {
            if (_isDisposed) return false;

            while (_queuedProviders.Count > 0)
            {
                var queued = _queuedProviders.Dequeue();
                if (queued is IDisposable disposableQueued)
                {
                    try
                    {
                        disposableQueued.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            _queuedProviders.Enqueue(nextProvider);
            var currentLength = GetCurrentLength(_currentProvider);
            var nextLength = GetLength(nextProvider);
            if (currentLength >= 0 && nextLength >= 0)
            {
                _length = currentLength + nextLength;
            }
            else
            {
                _length = -1;
            }
        }

        return true;
    }

    private bool SwitchToNextProvider(out bool changed)
    {
        changed = false;
        if (_isDisposed || _queuedProviders.Count == 0)
        {
            return false;
        }

        if (_currentProvider is IDisposable disposableCurrent)
        {
            try
            {
                disposableCurrent.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        var currentLength = GetCurrentLength(_currentProvider);
        if (currentLength >= 0)
        {
            _positionBase += currentLength;
        }

        _currentProvider = _queuedProviders.Dequeue();
        changed = true;
        return true;
    }
    private static long GetLength(IWaveProvider provider)
    {
        return provider switch
        {
            IWaveStream waveStream => waveStream.Length,
            WaveStream waveStream => waveStream.Length,
            _ => -1
        };
    }

    private static long GetCurrentPosition(IWaveProvider provider)
    {
        return provider switch
        {
            IWaveStream waveStream => waveStream.Position,
            WaveStream waveStream => waveStream.Position,
            _ => -1
        };
    }

    private static long GetCurrentLength(IWaveProvider provider)
    {
        return provider switch
        {
            IWaveStream waveStream => waveStream.Length,
            WaveStream waveStream => waveStream.Length,
            _ => -1
        };
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_currentProvider is IDisposable disposableCurrent)
            {
                try
                {
                    disposableCurrent.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            while (_queuedProviders.Count > 0)
            {
                var provider = _queuedProviders.Dequeue();
                if (provider is IDisposable disposableNext)
                {
                    try
                    {
                        disposableNext.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        GC.SuppressFinalize(this);
    }
}

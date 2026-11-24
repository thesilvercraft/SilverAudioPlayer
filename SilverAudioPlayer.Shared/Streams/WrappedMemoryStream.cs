using System.Collections.Concurrent;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedSharedMemoryStream : WrappedStream
{
    private SharedBuffer? _sharedBuffer;
    private readonly List<WeakReference<Stream>> _activeStreams = [];
    private readonly Lock _streamsLock = new();
    private bool _isDisposed;

    // Overriding the abstract property from WrappedStream
    public override MimeType MimeType { get; }
    public override bool IsSeekable => true;

    /// <summary>
    /// Initializes a new instance of WrappedSharedMemoryStream from a raw byte array.
    /// </summary>
    public WrappedSharedMemoryStream(byte[] data, MimeType? mt = null, bool copy = true)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        byte[] workingBuffer = copy ? data.ToArray() : data;
        _sharedBuffer = new SharedBuffer(workingBuffer);

        if (mt == null)
        {
            using var probeStream = new SharedMemoryStream(_sharedBuffer);
            // Matches your existing magic byte detection engine
            MimeType = MagicByteCombos.Match(probeStream, 0)?.MimeType
                       ?? throw new InvalidDataException("Could not determine MimeType from the provided data stream.");
        }
        else
        {
            MimeType = mt;
        }
    }

    /// <summary>
    /// Initializes a new instance using an already existing SharedBuffer context.
    /// </summary>
    public WrappedSharedMemoryStream(SharedBuffer sharedBuffer, MimeType mt)
    {
        _sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer));
        MimeType = mt ?? throw new ArgumentNullException(nameof(mt));
    }

    // Overriding the abstract factory method from WrappedStream
    public override Stream GetStream()
    {
        lock (_streamsLock)
        {
            if (_isDisposed || _sharedBuffer == null)
                throw new ObjectDisposedException(nameof(WrappedSharedMemoryStream),
                    "The wrapped memory stream has already been disposed.");

            var instantiatedStream = new SharedMemoryStream(_sharedBuffer);

            _activeStreams.RemoveAll(wr => !wr.TryGetTarget(out var target) || !target.CanRead);
            _activeStreams.Add(new WeakReference<Stream>(instantiatedStream));
            return instantiatedStream;
        }
    }

    // Overriding the abstract Dispose method from WrappedStream
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        List<Stream> toDispose = [];
        lock (_streamsLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            if (disposing)
            {
                foreach (var wr in _activeStreams)
                {
                    if (wr.TryGetTarget(out var trackedStream))
                    {
                        toDispose.Add(trackedStream);
                    }
                }

                _activeStreams.Clear();
            }

            // Invalidate the backing byte array reference completely
            _sharedBuffer?.Release();
            _sharedBuffer = null;
        }

        if (disposing)
        {
            foreach (var trackedStream in toDispose)
            {
                try
                {
                    trackedStream.Dispose();
                }
                catch
                {
                    /* Intentionally absorbed to prevent failures during cascading teardowns */
                }
            }
        }
    }

    ~WrappedSharedMemoryStream()
    {
        Dispose(false);
    }
}
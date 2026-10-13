using System.Collections.Concurrent;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedSharedMemoryStream : WrappedStream
{
    private SharedBuffer? _sharedBuffer;
    private readonly ConcurrentBag<Stream> _activeStreams = new();
    private bool _isDisposed;

    // Overriding the abstract property from WrappedStream
    public override MimeType MimeType { get; }

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
        if (_isDisposed || _sharedBuffer == null)
            throw new ObjectDisposedException(nameof(WrappedSharedMemoryStream), "The wrapped memory stream has already been disposed.");

        var instantiatedStream = new SharedMemoryStream(_sharedBuffer);

        // Track the stream in case it is invoked outside of the .Use() block helper
        _activeStreams.Add(instantiatedStream);
        return instantiatedStream;
    }

    // Overriding the abstract Dispose method from WrappedStream
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Safely clear out outstanding streams across concurrent decoder threads
                while (_activeStreams.TryTake(out var trackedStream))
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

            // Invalidate the backing byte array reference completely
            _sharedBuffer?.Release();
            _sharedBuffer = null;
            _isDisposed = true;
        }
    }

    ~WrappedSharedMemoryStream()
    {
        Dispose(false);
    }
}
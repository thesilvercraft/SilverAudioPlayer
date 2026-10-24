using System.Collections.Concurrent;
using Serilog;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedFileStream : WrappedStream, IDisposable
{
    private bool _disposedValue;
    private readonly ConcurrentBag<Stream> _streams = [];

    public override bool IsSeekable => true;
    public string URL { get; }
    public override MimeType MimeType { get; }

    public WrappedFileStream(string url)
    {
        URL = url ?? throw new ArgumentNullException(nameof(url));
        MimeType? mt;
        using (var tempStream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            mt = MagicByteCombos.Match(tempStream, 0)?.MimeType;
        }
        mt ??= KnownMimes.GetKnownMimeByExtension(new FileInfo(URL).Extension);
        MimeType = mt ?? KnownMimes.OctetMime;
    }

    private Stream InternalGetStream()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        var stream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        _streams.Add(stream);
        Log.Debug("Opened FileStream for {URL}, active stream count: {Count}", URL, _streams.Count);
        return stream;
    }

    public override Stream GetStream()
    {
        return InternalGetStream();
    }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        _disposedValue = true;
        if (!disposing) return;
        Log.Debug("Disposing WrappedFileStream for {URL} ({Count} underlying streams)", URL, _streams.Count);
        while (_streams.TryTake(out var stream))
        {
            try
            {
                stream.Dispose();
                Log.Debug("Disposed underlying FileStream for {URL}", URL);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing FileStream for {URL}", URL);
            }
        }
    }
}

using System.Collections.Concurrent;
using Serilog;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public class WrappedFileStream : WrappedStream, IDisposable
{
    private bool _disposedValue;
    private readonly List<WeakReference<Stream>> _streams = [];
    private readonly Lock _streamsLock = new();

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
        lock (_streamsLock)
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            _streams.RemoveAll(wr => !wr.TryGetTarget(out var target) || !target.CanRead);
            _streams.Add(new WeakReference<Stream>(stream));
            Log.Debug("Opened FileStream for {URL}, active stream count: {Count}", URL, _streams.Count);
        }

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
        List<Stream> toDispose = [];
        lock (_streamsLock)
        {
            if (_disposedValue) return;
            _disposedValue = true;
            if (disposing)
            {
                foreach (var wr in _streams)
                {
                    if (wr.TryGetTarget(out var stream))
                    {
                        toDispose.Add(stream);
                    }
                }

                _streams.Clear();
            }
        }

        if (!disposing) return;
        Log.Debug("Disposing WrappedFileStream for {URL} ({Count} underlying streams)", URL, toDispose.Count);
        foreach (var stream in toDispose)
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

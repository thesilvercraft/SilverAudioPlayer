using System.Collections.Concurrent;
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
        using (var tempStream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            mt = MagicByteCombos.Match(tempStream, 0)?.MimeType;
        }
        mt ??= KnownMimes.GetKnownMimeByExtension(new FileInfo(URL).Extension);
        MimeType = mt ?? KnownMimes.OctetMime;
    }

    private Stream InternalGetStream()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        var stream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.Read);
        _streams.Add(stream);
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
        while (_streams.TryTake(out var stream))
        {
            stream.Dispose();
        }
    }
}
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     Supports HTTP STREAMS THAT SUPPORT RANGE
/// </summary>
public class WrappedHttpStream : WrappedStream, IDisposable
{
    private readonly IRefreshableStream inside;

    public WrappedHttpStream(string url, HttpClient client) : this(new HttpRequestMessage(HttpMethod.Get, url), client)
    {
    }

    public WrappedHttpStream(HttpRequestMessage requestMessage, HttpClient client)
    {
        var probe = HttpSeekableStream.GetLengthAndRangeSupport(requestMessage, client);
        switch (probe)
        {
            case { SupportsRange: true }:
                inside = new HttpSeekableStream(requestMessage, client, probe) { IsParent = true };
                Seekable = true;
                break;
            case null:
                inside = new NoEndHttpStream(requestMessage, client);
                Seekable = false;
                CanCreateMultipleStreams = false;
                break;
            default:
                inside = new WrappedSusHttpStream(requestMessage, client);
                Seekable = true;
                break;
        }

        using var s = inside.GetStream();
        var mt = MagicByteCombos.Match(s, 0)?.MimeType;
        if (mt == null && requestMessage.RequestUri?.AbsolutePath is { } absPath)
        {
            mt = KnownMimes.GetKnownMimeByExtension(Path.GetExtension(absPath));
        }

        _MimeType = mt ?? KnownMimes.OctetMime;
    }

    public bool Seekable { get; }
    public override bool IsSeekable => Seekable;
    public override bool CanCreateMultipleStreams { get; } = true;
    public override MimeType MimeType => _MimeType;
    private MimeType _MimeType { get; }

    public string? Url => inside.RequestMessage?.RequestUri?.ToString();

    public override void Dispose()
    {
        inside.Dispose();
        GC.SuppressFinalize(this);
    }

    public override Stream GetStream()
    {
        var stream = inside.GetStream();
        return stream;
    }
}
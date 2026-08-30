using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     Supports HTTP STREAMS THAT SUPPORT RANGE
/// </summary>
public class WrappedHttpStream : WrappedStream, IDisposable
{
    private readonly IRefreshableStream inside;

    public WrappedHttpStream(string url, HttpClient client) : this(new HttpRequestMessage(HttpMethod.Get, url),client)
    {
    }

    public WrappedHttpStream(HttpRequestMessage requestMessage, HttpClient client)
    {
        var stype = HttpSeekableStream.GetLengthAndRangeSupport(requestMessage,client);
        switch (stype)
        {
            case { Item2: true }:
                inside = new HttpSeekableStream(requestMessage,client) { IsParent = true };
                Seekable = true;
                break;
            case null:
                inside = new NoEndHttpStream(requestMessage,client);
                break;
            default:
                inside = new WrappedSusHttpStream(requestMessage,client);
                break;
        }

        using var s = inside.GetStream();
        _MimeType = MagicByteCombos.Match(s, 0)?.MimeType;
    }

    public bool Seekable { get; }
    public override bool IsSeekable => Seekable;
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
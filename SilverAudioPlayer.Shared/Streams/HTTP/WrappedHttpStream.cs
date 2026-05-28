using System.Diagnostics;
using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

/// <summary>
///     Supports HTTP STREAMS THAT SUPPORT RANGE
/// </summary>
public class WrappedHttpStream : WrappedStream, IDisposable
{
    private readonly IRefreshableStream inside;

    public WrappedHttpStream(string url) : this(new HttpRequestMessage(HttpMethod.Get, url))
    {
    }

    public WrappedHttpStream(HttpRequestMessage requestMessage)
    {
        var stype = HttpSeekableStream.GetLengthAndRangeSupport(requestMessage);
        switch (stype)
        {
            case { Item2: true }:
                inside = new HttpSeekableStream(requestMessage) { IsParent = true };
                Seekable = true;
                break;
            case null:
                inside = new NoEndHttpStream(requestMessage);
                break;
            default:
                inside = new WrappedSusHttpStream(requestMessage);
                break;
        }

        using var s = inside.GetStream();
        _MimeType = MagicByteCombos.Match(s, 0)?.MimeType;
    }

    public bool Seekable { get; }
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
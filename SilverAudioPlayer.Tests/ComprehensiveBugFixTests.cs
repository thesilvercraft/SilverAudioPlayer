using System.Net;
using System.Text;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverAudioPlayer.Shared.SilverPlaylist;
using SilverMagicBytes;
using Xunit;

namespace SilverAudioPlayer.Tests;

public class HttpSeekableStreamTests
{
    private class FakeRangeHandler : HttpMessageHandler
    {
        private readonly byte[] _data;
        public int RequestCount { get; private set; }
        public bool FailOnChunk1Once { get; set; }
        private bool _hasFailedChunk1;

        public FakeRangeHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            if (range == null)
            {
                var fullResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_data)
                };
                fullResponse.Content.Headers.ContentLength = _data.Length;
                return Task.FromResult(fullResponse);
            }

            var from = (int)(range.From ?? 0);
            var to = (int)(range.To ?? (_data.Length - 1));
            to = Math.Min(to, _data.Length - 1);

            if (FailOnChunk1Once && from == HttpSeekableStream.BuffLength && !_hasFailedChunk1)
            {
                _hasFailedChunk1 = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            var length = Math.Max(0, to - from + 1);
            var slice = new byte[length];
            Array.Copy(_data, from, slice, 0, length);

            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(slice)
            };
            response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, to, _data.Length)
            {
                Unit = "bytes"
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void HttpSeekableStream_SeedsChunk0_AndReadsCorrectly()
    {
        var testData = new byte[HttpSeekableStream.BuffLength * 3];
        for (int i = 0; i < testData.Length; i++) testData[i] = (byte)(i % 251);

        var handler = new FakeRangeHandler(testData);
        var client = new HttpClient(handler);
        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test.mp3");

        var probe = HttpSeekableStream.GetLengthAndRangeSupport(req, client);
        Assert.NotNull(probe);
        Assert.True(probe.SupportsRange);
        Assert.Equal(testData.Length, probe.Length);
        Assert.NotNull(probe.InitialChunk);
        Assert.Equal(HttpSeekableStream.BuffLength, probe.InitialChunk.Value.Length);

        using var stream = new HttpSeekableStream(req, client, probe);
        Assert.True(stream.CanSeek);
        Assert.Equal(testData.Length, stream.Length);

        var buffer = new byte[100];
        var read = stream.Read(buffer, 0, 100);
        Assert.Equal(100, read);
        Assert.Equal(testData.AsSpan(0, 100).ToArray(), buffer);

        // Verify seeking
        stream.Seek(HttpSeekableStream.BuffLength + 10, SeekOrigin.Begin);
        Assert.Equal(HttpSeekableStream.BuffLength + 10, stream.Position);

        var b2 = new byte[10];
        var read2 = stream.Read(b2, 0, 10);
        Assert.Equal(10, read2);
        Assert.Equal(testData.AsSpan(HttpSeekableStream.BuffLength + 10, 10).ToArray(), b2);
    }

    [Fact]
    public void HttpSeekableStream_NegativeSeek_ThrowsIOException()
    {
        var testData = new byte[1000];
        var handler = new FakeRangeHandler(testData);
        var client = new HttpClient(handler);
        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test.mp3");
        var probe = HttpSeekableStream.GetLengthAndRangeSupport(req, client);

        using var stream = new HttpSeekableStream(req, client, probe);
        Assert.Throws<IOException>(() => stream.Seek(-10, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek(-1500, SeekOrigin.End));
    }

    [Fact]
    public void HttpSeekableStream_EvictsFaultedChunkFromCache()
    {
        var testData = new byte[HttpSeekableStream.BuffLength * 3];
        var handler = new FakeRangeHandler(testData) { FailOnChunk1Once = true };
        var client = new HttpClient(handler);
        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test.mp3");
        var probe = HttpSeekableStream.GetLengthAndRangeSupport(req, client);

        using var stream = new HttpSeekableStream(req, client, probe);
        stream.Position = HttpSeekableStream.BuffLength;

        var buf = new byte[10];
        Assert.ThrowsAny<Exception>(() => stream.Read(buf, 0, 10));

        // Now that the transient fault passed, reading should succeed because the failed task was evicted
        stream.Position = HttpSeekableStream.BuffLength;
        var read = stream.Read(buf, 0, 10);
        Assert.Equal(10, read);
    }

    [Fact]
    public void HttpSeekableStream_NonRangeServer_ThrowsOnSeek()
    {
        var testData = new byte[100];
        var handler = new FakeRangeHandler(testData);
        var client = new HttpClient(handler);
        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test.mp3");

        // Force non-range probe result
        var probe = new ProbeResult(testData.Length, supportsRange: false);
        using var stream = new HttpSeekableStream(req, client, probe);

        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Seek(10, SeekOrigin.Begin));
    }
}

public class MetadataComboAndLrcTests
{
    private class DummyMetadata : IMetadata
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Genre { get; set; }
        public int? Year { get; set; }
        public ulong? Bitrate { get; set; }
        public ulong? SampleRate { get; set; }
        public uint? Channels { get; set; }
        public int? TrackNumber { get; set; }
        public int? DiscNumber { get; set; }
        public string[]? Comments { get; set; }
        public double? Duration { get; set; }
        public string? Lyrics { get; set; }
        public IList<LyricPhrase>? SyncedLyrics { get; set; }
        public IReadOnlyList<IPicture>? Pictures { get; set; }
        public IDictionary<string, string>? Entries { get; set; }
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Fact]
    public void MetadataCombo_YearAndValuesHandledCorrectly()
    {
        var m1 = new DummyMetadata { Year = null, Bitrate = 128000, TrackNumber = 1, DiscNumber = 1 };
        var m2 = new DummyMetadata { Year = 9999, Bitrate = 320000, TrackNumber = 2, DiscNumber = 2 };
        var m3 = new DummyMetadata { Year = 2021, Bitrate = 256000, TrackNumber = null, DiscNumber = null };

        using var combo = new MetadataCombo([m1, m2, m3], []);
        Assert.Equal(2021, combo.Year);
        Assert.Equal(320000UL, combo.Bitrate);
        Assert.Equal(1, combo.TrackNumber);
        Assert.Equal(1, combo.DiscNumber);

        combo.Dispose();
        Assert.True(m1.IsDisposed);
        Assert.True(m2.IsDisposed);
        Assert.True(m3.IsDisposed);
    }

    [Fact]
    public void LrcParser_HandlesShortLines_AndCRLF_AndMalformedLines()
    {
        var lrcContent = "[offset: 50]\r\n[00:01.00]\r\n[00:02.00]Hello world\r\n[invalid line with no closing bracket\r\n";
        var lrc = LrcParser.Parse(lrcContent);

        Assert.Equal(-50, lrc.OffsetInMs);
        Assert.Equal(2, lrc.Count);
        Assert.Equal(1000 - 50, lrc[0].TimeStampInMilliSeconds);
        Assert.Equal("\n", lrc[0].Content);
        Assert.Equal(2000 - 50, lrc[1].TimeStampInMilliSeconds);
        Assert.Equal("Hello world\n", lrc[1].Content);
    }

    [Fact]
    public void LrcParser_EnhancedLrc_ParsesWordTimings()
    {
        var lrcContent = "[00:01.00]<00:01.20>Hello <00:01.80>world\n";
        var lrc = LrcParser.Parse(lrcContent);

        Assert.True(lrc.Count >= 2);
    }
}

public class StreamAndPoolTests
{
    [Fact]
    public void SharedMemoryStream_DisposedAndReadBounds()
    {
        var buffer = new SharedBuffer([1, 2, 3, 4, 5]);
        var stream = new SharedMemoryStream(buffer);

        var dest = new byte[3];
        var read = stream.Read(dest, 0, 3);
        Assert.Equal(3, read);
        Assert.Equal(3, stream.Position);

        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Read(dest, 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
    }

    [Fact]
    public void WrappedSharedMemoryStream_IsSeekable()
    {
        var ws = new WrappedSharedMemoryStream([1, 2, 3], mt: KnownMimes.MP3Mime);
        Assert.True(ws.IsSeekable);
    }

    [Fact]
    public void SharedPool_RemovesItemWhenLastRelianceDisposed()
    {
        var pool = new SharedPool<MemoryStream>();
        var ms = new MemoryStream([1, 2, 3]);
        var shared = pool.AddT(ms, "hash1");

        Assert.True(pool.ContainsHash("hash1"));

        var reliance1 = new RelianceOnShared<MemoryStream>(shared, pool);
        reliance1.Dispose();
        Assert.False(pool.ContainsHash("hash1"));
    }

    [Fact]
    public void SilverPlaylist_SerializeAndDeserializeBrotli_KeepsStreamOpen()
    {
        var playlist = new SilverPlayList
        {
            Version = "elephant",
            Song = []
        };

        using var ms = new MemoryStream();
        SilverPlaylistHelper.SerialiseBrotli(playlist, ms, leaveOpen: true);

        // Stream must still be open
        Assert.True(ms.CanSeek);
        ms.Position = 0;

        var deserialized = SilverPlaylistHelper.DeserializeBrotli(ms, leaveOpen: true);
        Assert.Equal("elephant", deserialized.Version);
        Assert.True(ms.CanRead);
    }
}

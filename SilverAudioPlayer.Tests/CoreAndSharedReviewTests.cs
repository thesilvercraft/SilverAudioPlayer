using System.Composition;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Core.MediatorHandlers;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverAudioPlayer.Shared.StandardRequests;
using SilverMagicBytes;
using Xunit;

namespace SilverAudioPlayer.Tests;

public class CoreAndSharedReviewTests
{
    private sealed class FakeLancerPlayer : IPlayLancer
    {
        public List<WrappedStream> EnqueuedStreams { get; } = [];
        public List<WrappedStream> ReplacedStreams { get; } = [];
        public bool IsTrackLoaded { get; private set; } = true;
        public PlaybackState State { get; set; } = PlaybackState.Playing;

        public IReadOnlyList<MimeType>? SupportedMimes => [KnownMimes.MP3Mime];

        public event EventHandler<object>? TrackEnd;
        event EventHandler<object>? IPlay.TrackPause { add { } remove { } }
        event EventHandler<TimeSpan>? IPlay.PositionChanged { add { } remove { } }
        public event EventHandler? NextStreamStarted;

        public void LoadStream(WrappedStream stream) => IsTrackLoaded = true;
        public void Play() => State = PlaybackState.Playing;
        public void Stop()
        {
            State = PlaybackState.Stopped;
            IsTrackLoaded = false;
            TrackEnd?.Invoke(this, EventArgs.Empty);
        }
        public void Pause() => State = PlaybackState.Paused;
        public void Resume() => State = PlaybackState.Playing;
        public uint? ChannelCount() => 2;
        public void SetVolume(byte volume) { }
        public TimeSpan GetPosition() => TimeSpan.Zero;
        public void SetPosition(TimeSpan position) { }
        public PlaybackState? GetPlaybackState() => State;
        public TimeSpan? Length() => TimeSpan.FromSeconds(10);
        public long? GetSampleRate() => 44100;
        public uint? GetBitsPerSample() => 16;

        public bool EnqueueNextStream(WrappedStream stream)
        {
            EnqueuedStreams.Add(stream);
            return true;
        }

        public bool ReplaceNextStream(WrappedStream stream)
        {
            ReplacedStreams.Add(stream);
            return true;
        }

        public void RaiseNextStreamStarted() => NextStreamStarted?.Invoke(this, EventArgs.Empty);
        public void RaiseTrackEnd() => TrackEnd?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class FakePlayProvider(Func<IPlay> factory) : IPlayProvider
    {
        public string Name => "FakeProvider";
        public string Description => "Fake";
        public WrappedStream? Icon => null;
        public Version? Version => new(1, 0, 0, 0);
        public string Licenses => "MIT";
        public List<Tuple<Uri, URLType>>? Links => null;
        public IReadOnlyList<MimeType>? SupportedMimes => [KnownMimes.MP3Mime];
        public bool CanPlayFile(WrappedStream stream) => true;
        public IPlay? GetPlayer(WrappedStream stream, IPlayerEnvironment? playerEnvironment = null) => factory();
        public Task OnStartup() => Task.CompletedTask;
    }

    private sealed class DummyEnv(Logic<PlayerContext> logic, IConfigPathProvider configPathProvider)
        : BaseEnvironment<PlayerContext>(logic, configPathProvider);

    private sealed class ImmediateUiBridge : IPlayerUiBridge
    {
        public void ResetUiScrollBar() { }
        public void SetScrollBarTextTo(TimeSpan totalDuration) { }
        public void HandleLateStageMetadataAndScrollBar() { }
        public void ShowMessageBox(string title, string message) { }
        public void PostToUi(Action action) => action();
    }

    private static Logic<PlayerContext> CreateLogic(Func<IPlay> playerFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediator>(new Mediator());
        var sp = services.BuildServiceProvider();
        var ctx = new PlayerContext { ServiceProvider = sp };
        var logic = new Logic<PlayerContext>(ctx, new ImmediateUiBridge())
        {
            PlayProviders = [new FakePlayProvider(playerFactory)],
            AutoSortEnabled = false
        };
        logic.PlayerEnvironment = new DummyEnv(logic, new ConfigPathProvider(Path.GetTempPath()));
        return logic;
    }

    private static Song CreateMemorySong(string name)
    {
        var stream = new WrappedSharedMemoryStream([1, 2, 3, 4], KnownMimes.MP3Mime);
        return new Song(stream, name);
    }

    [Fact]
    public async Task PlaybackEngine_StartPlayingAsync_EnqueuesNextSongForGapless()
    {
        var fakePlayer = new FakeLancerPlayer();
        var logic = CreateLogic(() => fakePlayer);
        using var song1 = CreateMemorySong("Track 1");
        using var song2 = CreateMemorySong("Track 2");
        using var song3 = CreateMemorySong("Track 3");

        logic.AddSong(song1, expectmore: true);
        logic.AddSong(song2, expectmore: true);
        logic.AddSong(song3, expectmore: true);

        await logic.StartPlayingAsync();
        await Task.Delay(50);

        Assert.Single(fakePlayer.EnqueuedStreams);
        Assert.Same(song2.Stream, fakePlayer.EnqueuedStreams[0]);

        fakePlayer.RaiseNextStreamStarted();
        await Task.Delay(50);

        Assert.Same(song2, logic.PlayerContext.CurrentSong);
        Assert.Equal(2, fakePlayer.EnqueuedStreams.Count);
        Assert.Same(song3.Stream, fakePlayer.EnqueuedStreams[1]);
    }

    [Fact]
    public async Task PlaybackEngine_ReorderNextSong_ReplacesEnqueuedGaplessStream()
    {
        var fakePlayer = new FakeLancerPlayer();
        var logic = CreateLogic(() => fakePlayer);
        using var song1 = CreateMemorySong("Track 1");
        using var song2 = CreateMemorySong("Track 2");
        using var song3 = CreateMemorySong("Track 3");

        logic.AddSong(song1, expectmore: true);
        logic.AddSong(song2, expectmore: true);
        logic.AddSong(song3, expectmore: true);

        await logic.StartPlayingAsync();
        await Task.Delay(50);

        Assert.Single(fakePlayer.EnqueuedStreams);
        Assert.Same(song2.Stream, fakePlayer.EnqueuedStreams[0]);

        logic.ReorderQueue(2, 1);
        await Task.Delay(50);

        Assert.Contains(song3.Stream, fakePlayer.ReplacedStreams);
    }

    [Fact]
    public async Task PlaybackEngine_StopDuringHandleSongChanging_DoesNotDoubleAdvance()
    {
        var fakePlayer = new FakeLancerPlayer();
        var logic = CreateLogic(() => fakePlayer);
        using var song1 = CreateMemorySong("Track 1");
        using var song2 = CreateMemorySong("Track 2");
        using var song3 = CreateMemorySong("Track 3");

        logic.AddSong(song1, expectmore: true);
        logic.AddSong(song2, expectmore: true);
        logic.AddSong(song3, expectmore: true);

        await logic.StartPlayingAsync();
        logic.HandleSongChanging(song2);
        await Task.Delay(100);

        Assert.Same(song2, logic.PlayerContext.CurrentSong);
    }

    [Fact]
    public void Song_DuplicateInstances_HaveSameGuidButDistinctInstanceEquality()
    {
        using var s1 = CreateMemorySong("Same");
        using var s2 = CreateMemorySong("Same");

        Assert.Equal(s1.Guid, s2.Guid);
        Assert.True(s1.Equals(s1.Guid));
        Assert.False(s1.Equals(s2));

        var logic = CreateLogic(() => new FakeLancerPlayer());
        logic.AddSong(s1, expectmore: true);
        logic.AddSong(s2, expectmore: true);
        logic.PlayerContext.CurrentSong = s1;

        logic.Next();
        Assert.Same(s2, logic.PlayerContext.CurrentSong);
    }

    [Fact]
    public async Task QueueManager_SortDoesNotOverwriteConcurrentAdditions()
    {
        var logic = CreateLogic(() => new FakeLancerPlayer());
        logic.AutoSortEnabled = true;

        using var s1 = CreateMemorySong("A");
        using var s2 = CreateMemorySong("B");
        using var s3 = CreateMemorySong("C");

        logic.AddSong(s1, expectmore: false); // triggers debounced sort
        await Task.Delay(30);
        logic.AddSong(s2, expectmore: true);  // concurrent modification during sort delay
        logic.AddSong(s3, expectmore: false);

        await Task.Delay(300);

        var copy = logic.GetQueueCopy();
        Assert.Equal(3, copy.Count);
        Assert.Contains(s1, copy);
        Assert.Contains(s2, copy);
        Assert.Contains(s3, copy);
    }

    [Fact]
    public void LrcParser_HandlesRoundingStackedTimestampsAndBottomOffset()
    {
        var lrc = """
                  [00:01.29]First line
                  [00:02.00][00:10.00]Chorus
                  [00:03.00]<not-a-timestamp>Text
                  [offset: 100]
                  """;

        var parsed = LrcParser.Parse(lrc);
        Assert.Equal(-100, parsed.OffsetInMs);
        Assert.Equal(4, parsed.Count);
        Assert.Equal(1190, parsed[0].TimeStampInMilliSeconds);
        Assert.Equal(1900, parsed[1].TimeStampInMilliSeconds);
        Assert.Equal("Chorus\n", parsed[1].Content);
        Assert.Equal(2900, parsed[2].TimeStampInMilliSeconds);
        Assert.Equal("<not-a-timestamp>Text\n", parsed[2].Content);
        Assert.Equal(9900, parsed[3].TimeStampInMilliSeconds);
        Assert.Equal("Chorus\n", parsed[3].Content);
    }

    [Fact]
    public void ReplayGainHandler_IsCaseInsensitive()
    {
        var handler = new ReplayGainHandler();
        var meta = new TestMetadata
        {
            Entries = new Dictionary<string, string>
            {
                ["replaygain_track_gain"] = "-6.5 dB",
                ["replaygain_track_peak"] = "0.95"
            }
        };
        using var song = new Song(new WrappedSharedMemoryStream([1, 2, 3], KnownMimes.MP3Mime), "rg", meta);
        var query = new QueryEventArgs(RequestReplayGainInfo.Instance);

        handler.HandleMediatorQuery(query, song);

        Assert.True(query.Handled);
        var info = Assert.IsType<ReplayGainInfo>(query.Result);
        Assert.True(info.Success);
        Assert.Equal(-6.5f, info.Gain);
        Assert.Equal(0.95f, info.Peak);
    }

    private sealed class TestMetadata : Metadata
    {
        public override void Dispose() { }
    }

    [Fact]
    public void WrappedEmbeddedResourceStream_MimeTypePopulatedBeforeGetStream()
    {
        var asm = typeof(Song).Assembly;
        var resourceName = asm.GetManifestResourceNames().FirstOrDefault();
        if (resourceName == null) return; // skip if no embedded resources exist in Shared

        using var wrapped = new WrappedEmbeddedResourceStream(asm, resourceName);
        Assert.NotNull(wrapped.MimeType);
    }

    private sealed class TrackingHttpMessageHandler : HttpMessageHandler
    {
        public int DisposedResponses { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new TrackedContent(this)
            };
            return Task.FromResult(msg);
        }

        private sealed class TrackedContent(TrackingHttpMessageHandler owner) : ByteArrayContent([1, 2, 3, 4])
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing) owner.DisposedResponses++;
                base.Dispose(disposing);
            }
        }
    }

    [Fact]
    public void NoEndHttpStream_DisposesHttpResponseWhenStreamDisposed()
    {
        var handler = new TrackingHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var noEnd = new NoEndHttpStream(new HttpRequestMessage(HttpMethod.Get, "http://localhost/live"), client);

        using (var s = noEnd.GetStream())
        {
            var buf = new byte[4];
            Assert.Equal(4, s.Read(buf, 0, 4));
        }

        Assert.Equal(1, handler.DisposedResponses);
    }

    [Fact]
    public void CommandPalette_And_LevenshteinDistance_WorkCorrectly()
    {
        var palette = new CommandPalette();
        palette.AddItem(new CommandPaletteItem("Play Track", "play", _ => { }));
        palette.AddItem(new CommandPaletteItem("Stop Track", "stop", _ => { }));

        var all = palette.Search("");
        all.Clear(); // mutating returned list must not affect palette

        Assert.Equal(2, palette.Search("").Count);
        var sorted = palette.Search("play");
        Assert.Equal("Play Track", sorted[0].HumanName);
        Assert.Equal(3, LevenshteinDistance.Compute("kitten", "sitting"));
    }

    [Fact]
    public void WrappedSusHttpStream_BuffersResponseAndReadsMultipleStreams()
    {
        var handler = new TrackingHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var sus = new WrappedSusHttpStream(new HttpRequestMessage(HttpMethod.Get, "http://localhost/track"), client);

        Assert.Equal(1, handler.DisposedResponses);

        using var s1 = sus.GetStream();
        using var s2 = sus.GetStream();
        var buf1 = new byte[4];
        var buf2 = new byte[4];
        Assert.Equal(4, s1.Read(buf1, 0, 4));
        Assert.Equal(4, s2.Read(buf2, 0, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf1);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf2);
    }

    [Fact]
    public void SilverPlaylistHelper_ValidatesMagicHeaderAndRoundTrips()
    {
        using var invalid = new MemoryStream("Bad"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            SilverAudioPlayer.Shared.SilverPlaylist.SilverPlaylistHelper.DeserializeBrotli(invalid));

        var playlist = new SilverAudioPlayer.Shared.SilverPlaylist.SilverPlayList
        {
            Version = "elephant",
            Name = "Test"
        };
        using var ms = new MemoryStream();
        SilverAudioPlayer.Shared.SilverPlaylist.SilverPlaylistHelper.SerialiseBrotli(playlist, ms, leaveOpen: true);
        ms.Position = 0;

        var roundTripped = SilverAudioPlayer.Shared.SilverPlaylist.SilverPlaylistHelper.DeserializeBrotli(ms);
        Assert.Equal("Test", roundTripped.Name);
    }
}
